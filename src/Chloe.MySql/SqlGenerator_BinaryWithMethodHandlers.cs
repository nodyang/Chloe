﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using System.Reflection;

namespace Chloe.MySql
{
    partial class SqlGenerator : SqlGeneratorBase
    {
        static Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>> InitBinaryWithMethodHandlers()
        {
            var binaryWithMethodHandlers = new Dictionary<MethodInfo, Action<DbBinaryExpression, SqlGeneratorBase>>();
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_String_String, StringConcat);
            binaryWithMethodHandlers.Add(PublicConstants.MethodInfo_String_Concat_Object_Object, StringConcat);

            var ret = PublicHelper.Clone(binaryWithMethodHandlers);
            return ret;
        }

        static void StringConcat(DbBinaryExpression exp, SqlGeneratorBase generator)
        {
            List<DbExpression> operands = new List<DbExpression>();
            operands.Add(exp.Right);

            DbExpression left = exp.Left;
            DbAddExpression e = null;
            while ((e = (left as DbAddExpression)) != null && (e.Method == PublicConstants.MethodInfo_String_Concat_String_String || e.Method == PublicConstants.MethodInfo_String_Concat_Object_Object))
            {
                operands.Add(e.Right);
                left = e.Left;
            }

            operands.Add(left);

            DbExpression whenExp = null;
            List<DbExpression> operandExps = new List<DbExpression>(operands.Count);
            for (int i = operands.Count - 1; i >= 0; i--)
            {
                DbExpression operand = operands[i];
                DbExpression opBody = operand;
                if (opBody.Type != PublicConstants.TypeOfString)
                {
                    // 需要 cast type
                    opBody = new DbConvertExpression(PublicConstants.TypeOfString, opBody);
                }

                DbExpression equalNullExp = new DbEqualExpression(opBody, PublicConstants.DbConstant_Null_String);

                if (whenExp == null)
                    whenExp = equalNullExp;
                else
                    whenExp = new DbAndExpression(whenExp, equalNullExp);

                operandExps.Add(opBody);
            }

            generator.SqlBuilder.Append("CASE", " WHEN ");
            whenExp.Accept(generator);
            generator.SqlBuilder.Append(" THEN ");
            DbConstantExpression.Null.Accept(generator);
            generator.SqlBuilder.Append(" ELSE ");

            generator.SqlBuilder.Append("CONCAT(");

            for (int i = 0; i < operandExps.Count; i++)
            {
                if (i > 0)
                    generator.SqlBuilder.Append(",");

                generator.SqlBuilder.Append("IFNULL(");
                operandExps[i].Accept(generator);
                generator.SqlBuilder.Append(",");
                DbConstantExpression.StringEmpty.Accept(generator);
                generator.SqlBuilder.Append(")");
            }

            generator.SqlBuilder.Append(")");

            generator.SqlBuilder.Append(" END");
        }
    }
}
