﻿using Chloe.DbExpressions;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.QueryExpressions;
using Chloe.Utility;
using System.Linq.Expressions;
using Chloe.Visitors;

namespace Chloe.Query.QueryState
{
    internal sealed class RootQueryState : QueryStateBase
    {
        RootQueryExpression _rootQueryExp;
        public RootQueryState(RootQueryExpression rootQueryExp, ScopeParameterDictionary scopeParameters, StringSet scopeTables) : this(rootQueryExp, scopeParameters, scopeTables, null)
        {
        }
        public RootQueryState(RootQueryExpression rootQueryExp, ScopeParameterDictionary scopeParameters, StringSet scopeTables, Func<string, string> tableAliasGenerator)
          : base(new QueryContext((DbContextProvider)rootQueryExp.Provider), CreateQueryModel(rootQueryExp, scopeParameters, scopeTables, tableAliasGenerator))
        {
            this._rootQueryExp = rootQueryExp;
        }

        public override QueryModel ToFromQueryModel()
        {
            QueryModel newQueryModel = new QueryModel(this.QueryModel.Options, this.QueryModel.ScopeParameters, this.QueryModel.ScopeTables);
            newQueryModel.FromTable = this.QueryModel.FromTable;
            newQueryModel.ResultModel = this.QueryModel.ResultModel;
            newQueryModel.Condition = this.QueryModel.Condition;
            if (!this.QueryModel.Options.IgnoreFilters)
            {
                newQueryModel.GlobalFilters.AppendRange(this.QueryModel.GlobalFilters);
                newQueryModel.ContextFilters.AppendRange(this.QueryModel.ContextFilters);
            }

            return newQueryModel;
        }

        public override IQueryState Accept(IncludeExpression exp)
        {
            ComplexObjectModel owner = (ComplexObjectModel)this.QueryModel.ResultModel;
            owner.Include(exp.NavigationNode, this.QueryModel, false);

            return this;
        }
        public override IQueryState Accept(IgnoreAllFiltersExpression exp)
        {
            this.QueryModel.Options.IgnoreFilters = true;
            return this;
        }

        public override JoinQueryResult ToJoinQueryResult(JoinType joinType, LambdaExpression conditionExpression, ScopeParameterDictionary scopeParameters, StringSet scopeTables, Func<string, string> tableAliasGenerator)
        {
            if (this.QueryModel.Condition != null)
            {
                return base.ToJoinQueryResult(joinType, conditionExpression, scopeParameters, scopeTables, tableAliasGenerator);
            }

            scopeParameters = scopeParameters.Clone(conditionExpression.Parameters.Last(), this.QueryModel.ResultModel);
            DbExpression condition = GeneralExpressionParser.Parse(conditionExpression, scopeParameters, scopeTables);
            DbJoinTableExpression joinTable = new DbJoinTableExpression(joinType.AsDbJoinType(), this.QueryModel.FromTable.Table, condition);

            if (!this.QueryModel.Options.IgnoreFilters)
            {
                joinTable.Condition = joinTable.Condition.And(this.QueryModel.ContextFilters).And(this.QueryModel.GlobalFilters);
            }

            JoinQueryResult result = new JoinQueryResult();
            result.ResultModel = this.QueryModel.ResultModel;
            result.JoinTable = joinTable;

            return result;
        }

        static QueryModel CreateQueryModel(RootQueryExpression rootQueryExp, ScopeParameterDictionary scopeParameters, StringSet scopeTables, Func<string, string> tableAliasGenerator)
        {
            DbContextProvider dbContext = (DbContextProvider)rootQueryExp.Provider;
            Type entityType = rootQueryExp.ElementType;

            if (entityType.IsAbstract || entityType.IsInterface)
                throw new ArgumentException("The type of input can not be abstract class or interface.");

            QueryModel queryModel = new QueryModel(scopeParameters, scopeTables);

            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);

            DbTable dbTable = typeDescriptor.GenDbTable(rootQueryExp.ExplicitTable);
            string alias = null;
            if (tableAliasGenerator != null)
                alias = tableAliasGenerator(dbTable.Name);
            else
                alias = queryModel.GenerateUniqueTableAlias(dbTable.Name);

            queryModel.FromTable = CreateRootTable(dbTable, alias, rootQueryExp.Lock);

            ComplexObjectModel model = typeDescriptor.GenObjectModel(alias, queryModel.Options);
            model.DependentTable = queryModel.FromTable;

            queryModel.ResultModel = model;

            List<LambdaExpression> contextFilters = dbContext.QueryFilters.FindValue(entityType) ?? new List<LambdaExpression>();
            ParseFilters(queryModel, typeDescriptor.Definition.Filters, contextFilters);

            return queryModel;
        }
        static DbFromTableExpression CreateRootTable(DbTable table, string alias, LockType lockType)
        {
            DbTableExpression tableExp = new DbTableExpression(table);
            DbTableSegment tableSeg = new DbTableSegment(tableExp, alias, lockType);
            var fromTableExp = new DbFromTableExpression(tableSeg);
            return fromTableExp;
        }
        static void ParseFilters(QueryModel queryModel, IList<LambdaExpression> globalFilters, IList<LambdaExpression> contextFilters)
        {
            for (int i = 0; i < globalFilters.Count; i++)
            {
                queryModel.GlobalFilters.Add(ParseFilter(queryModel, globalFilters[i]));
            }

            for (int i = 0; i < contextFilters.Count; i++)
            {
                queryModel.ContextFilters.Add(ParseFilter(queryModel, contextFilters[i]));
            }
        }
        static DbExpression ParseFilter(QueryModel queryModel, LambdaExpression filter)
        {
            ScopeParameterDictionary scopeParameters = queryModel.ScopeParameters.Clone(filter.Parameters[0], queryModel.ResultModel);
            DbExpression filterCondition = FilterPredicateParser.Parse(filter, scopeParameters, queryModel.ScopeTables);
            return filterCondition;
        }
    }
}
