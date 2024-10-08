﻿using System.Linq.Expressions;

namespace Chloe.QueryExpressions
{
    public class WhereExpression : QueryExpression
    {
        LambdaExpression _predicate;
        public WhereExpression(Type elementType, QueryExpression prevExpression, LambdaExpression predicate) : base(QueryExpressionType.Where, elementType, prevExpression)
        {
            this._predicate = predicate;
        }
        public LambdaExpression Predicate
        {
            get { return this._predicate; }
        }
        public override T Accept<T>(IQueryExpressionVisitor<T> visitor)
        {
            return visitor.VisitWhere(this);
        }
    }
}
