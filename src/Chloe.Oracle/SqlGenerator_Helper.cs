﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.Reflection;

namespace Chloe.Oracle
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        void LeftBracket()
        {
            this.SqlBuilder.Append("(");
        }
        void RightBracket()
        {
            this.SqlBuilder.Append(")");
        }

        static string GenParameterName(int ordinal)
        {
            if (ordinal < CacheParameterNames.Count)
            {
                return CacheParameterNames[ordinal];
            }

            return UtilConstants.ParameterNamePrefix + ordinal.ToString();
        }
        static string GenRowNumberName(List<DbColumnSegment> columns)
        {
            int ROW_NUMBER_INDEX = 1;
            string row_numberName = "ROW_NUMBER_0";
            while (columns.Any(a => string.Equals(a.Alias, row_numberName, StringComparison.OrdinalIgnoreCase)))
            {
                row_numberName = "ROW_NUMBER_" + ROW_NUMBER_INDEX.ToString();
                ROW_NUMBER_INDEX++;
            }

            return row_numberName;
        }

        static DbExpression EnsureDbExpressionReturnCSharpBoolean(DbExpression exp)
        {
            return DbValueExpressionTransformer.Transform(exp);
        }

        static bool TryGetCastTargetDbTypeString(Type sourceType, Type targetType, out string dbTypeString, bool throwNotSupportedException = true)
        {
            dbTypeString = null;

            sourceType = ReflectionExtension.GetUnderlyingType(sourceType);
            targetType = ReflectionExtension.GetUnderlyingType(targetType);

            if (sourceType == targetType)
                return false;

            if (CastTypeMap.TryGetValue(targetType, out dbTypeString))
            {
                return true;
            }

            if (throwNotSupportedException)
                throw new NotSupportedException(PublicHelper.AppendNotSupportedCastErrorMsg(sourceType, targetType));
            else
                return false;
        }

        public static void DbFunction_DATEADD(SqlGeneratorBase generator, string interval, DbMethodCallExpression exp)
        {
            /*
             * Just support hour/minute/second
             * systimestamp + numtodsinterval(1,'HOUR')
             * sysdate + numtodsinterval(50,'MINUTE')
             * sysdate + numtodsinterval(45,'SECOND')
             */
            generator.SqlBuilder.Append("(");
            exp.Object.Accept(generator);
            generator.SqlBuilder.Append(" + ");
            generator.SqlBuilder.Append("NUMTODSINTERVAL(");
            exp.Arguments[0].Accept(generator);
            generator.SqlBuilder.Append(",'");
            generator.SqlBuilder.Append(interval);
            generator.SqlBuilder.Append("')");
            generator.SqlBuilder.Append(")");
        }
        public static void DbFunction_DATEPART(SqlGeneratorBase generator, string interval, DbExpression exp, bool castToTimestamp = false)
        {
            /* cast(to_char(sysdate,'yyyy') as number) */
            generator.SqlBuilder.Append("CAST(TO_CHAR(");
            if (castToTimestamp)
            {
                BuildCastState(generator, exp, "TIMESTAMP");
            }
            else
                exp.Accept(generator);
            generator.SqlBuilder.Append(",'");
            generator.SqlBuilder.Append(interval);
            generator.SqlBuilder.Append("') AS NUMBER)");
        }
        public static void DbFunction_DATEDIFF(SqlGeneratorBase generator, string interval, DbExpression startDateTimeExp, DbExpression endDateTimeExp)
        {
            throw new NotSupportedException("DATEDIFF is not supported.");
        }

        #region AggregateFunction
        public static void Aggregate_Count(SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("COUNT(1)");
        }
        public static void Aggregate_Count(SqlGeneratorBase generator, DbExpression arg)
        {
            generator.SqlBuilder.Append("COUNT(");
            arg.Accept(generator);
            generator.SqlBuilder.Append(")");
        }
        public static void Aggregate_LongCount(SqlGeneratorBase generator)
        {
            Aggregate_Count(generator);
        }
        public static void Aggregate_LongCount(SqlGeneratorBase generator, DbExpression arg)
        {
            Aggregate_Count(generator, arg);
        }
        public static void Aggregate_Max(SqlGeneratorBase generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunction(generator, exp, retType, "MAX", false);
        }
        public static void Aggregate_Min(SqlGeneratorBase generator, DbExpression exp, Type retType)
        {
            AppendAggregateFunction(generator, exp, retType, "MIN", false);
        }
        public static void Aggregate_Sum(SqlGeneratorBase generator, DbExpression exp, Type retType)
        {
            if (retType.IsNullable())
            {
                AppendAggregateFunction(generator, exp, retType, "SUM", false);
            }
            else
            {
                generator.SqlBuilder.Append("NVL(");
                AppendAggregateFunction(generator, exp, retType, "SUM", false);
                generator.SqlBuilder.Append(",");
                generator.SqlBuilder.Append("0");
                generator.SqlBuilder.Append(")");
            }
        }
        public static void Aggregate_Average(SqlGeneratorBase generator, DbExpression exp, Type retType)
        {
            generator.SqlBuilder.Append("TRUNC", "(");
            AppendAggregateFunction(generator, exp, retType, "AVG", false);
            generator.SqlBuilder.Append(",7)");
        }

        static void AppendAggregateFunction(SqlGeneratorBase generator, DbExpression exp, Type retType, string functionName, bool withCast)
        {
            string dbTypeString = null;
            if (withCast == true)
            {
                Type underlyingType = ReflectionExtension.GetUnderlyingType(retType);
                if (CastTypeMap.TryGetValue(underlyingType, out dbTypeString))
                {
                    generator.SqlBuilder.Append("CAST(");
                }
            }

            generator.SqlBuilder.Append(functionName, "(");
            exp.Accept(generator);
            generator.SqlBuilder.Append(")");

            if (dbTypeString != null)
            {
                generator.SqlBuilder.Append(" AS ", dbTypeString, ")");
            }
        }
        #endregion


        static void CopyColumnSegments(List<DbColumnSegment> sourceList, List<DbColumnSegment> destinationList, DbTable newTable)
        {
            for (int i = 0; i < sourceList.Count; i++)
            {
                DbColumnSegment newColumnSeg = CloneColumnSegment(sourceList[i], newTable);
                destinationList.Add(newColumnSeg);
            }
        }
        static DbColumnSegment CloneColumnSegment(DbColumnSegment rawColumnSeg, DbTable newBelongTable)
        {
            DbColumnAccessExpression columnAccessExp = new DbColumnAccessExpression(newBelongTable, DbColumn.MakeColumn(rawColumnSeg.Body, rawColumnSeg.Alias));
            DbColumnSegment newColumnSeg = new DbColumnSegment(columnAccessExp, rawColumnSeg.Alias);

            return newColumnSeg;
        }
        static void AppendLimitCondition(DbSqlQueryExpression sqlQuery, int limitCount)
        {
            DbLessThanExpression lessThanExp = new DbLessThanExpression(OracleSemantics.DbMemberExpression_ROWNUM, new DbConstantExpression(limitCount + 1));

            DbExpression condition = lessThanExp;
            if (sqlQuery.Condition != null)
                condition = new DbAndExpression(sqlQuery.Condition, condition);

            sqlQuery.Condition = condition;
        }
        static DbSqlQueryExpression WrapSqlQuery(DbSqlQueryExpression sqlQuery, DbTable table, List<DbColumnSegment> columnSegments = null)
        {
            DbSubqueryExpression subquery = new DbSubqueryExpression(sqlQuery);

            DbSqlQueryExpression newSqlQuery = new DbSqlQueryExpression(sqlQuery.Type, (columnSegments ?? subquery.SqlQuery.ColumnSegments).Count, 0, 0);

            DbTableSegment tableSeg = new DbTableSegment(subquery, table.Name, LockType.Unspecified);
            DbFromTableExpression fromTableExp = new DbFromTableExpression(tableSeg);

            newSqlQuery.Table = fromTableExp;

            CopyColumnSegments(columnSegments ?? subquery.SqlQuery.ColumnSegments, newSqlQuery.ColumnSegments, table);

            return newSqlQuery;
        }
        static DbSqlQueryExpression CloneWithoutLimitInfo(DbSqlQueryExpression sqlQuery, string wraperTableName = "T")
        {
            DbSqlQueryExpression newSqlQuery = new DbSqlQueryExpression(sqlQuery.Type, sqlQuery.ColumnSegments.Count, sqlQuery.GroupSegments.Count, sqlQuery.Orderings.Count);
            newSqlQuery.Table = sqlQuery.Table;
            newSqlQuery.ColumnSegments.AddRange(sqlQuery.ColumnSegments);
            newSqlQuery.Condition = sqlQuery.Condition;

            newSqlQuery.GroupSegments.AddRange(sqlQuery.GroupSegments);
            newSqlQuery.HavingCondition = sqlQuery.HavingCondition;

            newSqlQuery.Orderings.AddRange(sqlQuery.Orderings);

            if (sqlQuery.Orderings.Count > 0 || sqlQuery.GroupSegments.Count > 0)
            {
                newSqlQuery = WrapSqlQuery(newSqlQuery, new DbTable(wraperTableName));
            }

            return newSqlQuery;
        }
    }
}
