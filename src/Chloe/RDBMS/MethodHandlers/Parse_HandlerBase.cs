﻿using Chloe.DbExpressions;

namespace Chloe.RDBMS.MethodHandlers
{
    public class Parse_HandlerBase : MethodHandlerBase
    {
        public override bool CanProcess(DbMethodCallExpression exp)
        {
            if (exp.Arguments.Count != 1)
                return false;

            DbExpression arg = exp.Arguments[0];
            if (arg.Type != PublicConstants.TypeOfString)
                return false;

            Type retType = exp.Method.ReturnType;
            if (exp.Method.DeclaringType != retType)
                return false;

            return true;
        }
        public override void Process(DbMethodCallExpression exp, SqlGeneratorBase generator)
        {
            DbExpression arg = exp.Arguments[0];
            DbExpression e = new DbConvertExpression(exp.Method.ReturnType, arg);
            if (exp.Method.ReturnType == PublicConstants.TypeOfBoolean)
            {
                e.Accept(generator);
                generator.SqlBuilder.Append(" = ");
                DbConstantExpression.True.Accept(generator);
            }
            else
                e.Accept(generator);
        }
    }
}
