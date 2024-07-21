﻿using Chloe.Visitors;
using Chloe.Reflection;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.Extensions
{
    public static class ExpressionExtension
    {
        public static bool IsEvaluable(this Expression exp)
        {
            return ExpressionEvaluableJudge.CanEvaluate(exp);
        }
        public static object Evaluate(this Expression exp)
        {
            return ExpressionEvaluator.Evaluate(exp);
        }

        public static BinaryExpression Assign(MemberInfo propertyOrField, Expression instance, Expression value)
        {
            var member = MakeMemberExpression(propertyOrField, instance);
            var setValue = Expression.Assign(member, value);
            return setValue;
        }

        public static MemberExpression MakeMemberExpression(MemberInfo propertyOrField, Expression instance)
        {
            if (propertyOrField.MemberType == MemberTypes.Property)
            {
                var prop = Expression.Property(instance, (PropertyInfo)propertyOrField);
                return prop;
            }

            if (propertyOrField.MemberType == MemberTypes.Field)
            {
                var field = Expression.Field(instance, (FieldInfo)propertyOrField);
                return field;
            }

            throw new ArgumentException();
        }

        public static LambdaExpression AndAlso(this LambdaExpression a, LambdaExpression b)
        {
            if (a == null)
                return b;
            if (b == null)
                return a;

            Type rootType = a.Parameters[0].Type;
            var memberParam = Expression.Parameter(rootType, "root");
            var aNewBody = ParameterExpressionReplacer.Replace(a.Body, memberParam);
            var bNewBody = ParameterExpressionReplacer.Replace(b.Body, memberParam);
            var newBody = Expression.AndAlso(aNewBody, bNewBody);
            var lambda = Expression.Lambda(a.Type, newBody, memberParam);
            return lambda;
        }

        internal static bool IsDerivedFromParameter(this MemberExpression exp)
        {
            ParameterExpression p;
            return IsDerivedFromParameter(exp, out p);
        }
        internal static bool IsDerivedFromParameter(this MemberExpression exp, out ParameterExpression p)
        {
            p = null;

            MemberExpression memberExp = exp;
            Expression prevExp;
            do
            {
                prevExp = memberExp.Expression;
                memberExp = prevExp as MemberExpression;
            } while (memberExp != null);

            if (prevExp == null)/* 静态属性访问 */
                return false;

            if (prevExp.NodeType == ExpressionType.Parameter)
            {
                p = (ParameterExpression)prevExp;
                return true;
            }

            /* 当实体继承于某个接口或类时，会有这种情况 */
            if (prevExp.NodeType == ExpressionType.Convert)
            {
                prevExp = ((UnaryExpression)prevExp).Operand;
                if (prevExp.NodeType == ExpressionType.Parameter)
                {
                    p = (ParameterExpression)prevExp;
                    return true;
                }
            }

            return false;
        }

        public static Expression StripQuotes(this Expression exp)
        {
            while (exp.NodeType == ExpressionType.Quote)
            {
                exp = ((UnaryExpression)exp).Operand;
            }
            return exp;
        }

        public static Expression StripConvert(this Expression exp)
        {
            Expression operand = exp;
            while (operand.NodeType == ExpressionType.Convert || operand.NodeType == ExpressionType.ConvertChecked)
            {
                operand = ((UnaryExpression)operand).Operand;
            }
            return operand;
        }

        public static Stack<MemberExpression> Reverse(this MemberExpression exp)
        {
            var stack = new Stack<MemberExpression>();
            do
            {
                stack.Push(exp);
                exp = exp.Expression as MemberExpression;
            } while (exp != null);

            return stack;
        }

        public static Expression MakeWrapperAccess(object value, Type targetType)
        {
            if (value == null)
            {
                if (targetType != null)
                    return Expression.Constant(value, targetType);
                else
                    return Expression.Constant(value, typeof(object));
            }

            object wrapper = WrapValue(value);
            ConstantExpression wrapperConstantExp = Expression.Constant(wrapper);
            Expression ret = Expression.MakeMemberAccess(wrapperConstantExp, wrapper.GetType().GetProperty("Value"));

            if (ret.Type != targetType)
            {
                ret = Expression.Convert(ret, targetType);
            }

            return ret;
        }

        static object WrapValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            Type valueType = value.GetType();

            if (valueType == PublicConstants.TypeOfString)
            {
                return new ConstantWrapper<string>((string)value);
            }

            if (valueType == PublicConstants.TypeOfInt32)
            {
                return new ConstantWrapper<int>((int)value);
            }

            if (valueType == PublicConstants.TypeOfInt64)
            {
                return new ConstantWrapper<long>((long)value);
            }

            if (valueType == PublicConstants.TypeOfByte)
            {
                return new ConstantWrapper<byte>((byte)value);
            }

            if (valueType == PublicConstants.TypeOfSingle)
            {
                return new ConstantWrapper<float>((float)value);
            }

            if (valueType == PublicConstants.TypeOfDouble)
            {
                return new ConstantWrapper<double>((double)value);
            }

            if (valueType == PublicConstants.TypeOfDecimal)
            {
                return new ConstantWrapper<decimal>((decimal)value);
            }

            if (valueType == PublicConstants.TypeOfGuid)
            {
                return new ConstantWrapper<Guid>((Guid)value);
            }

            if (valueType == PublicConstants.TypeOfDateTime)
            {
                return new ConstantWrapper<DateTime>((DateTime)value);
            }

            if (valueType == PublicConstants.TypeOfBoolean)
            {
                return new ConstantWrapper<bool>((bool)value);
            }

            if (valueType == PublicConstants.TypeOfChar)
            {
                return new ConstantWrapper<char>((char)value);
            }

            Type wrapperType = typeof(ConstantWrapper<>).MakeGenericType(valueType);
            ConstructorInfo constructor = wrapperType.GetConstructor(new Type[] { valueType });
            return constructor.FastCreateInstance(new object[] { value });
        }
    }
}
