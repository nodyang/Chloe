﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SQLite.PropertyHandlers
{
    class DayOfWeek_Handler : DayOfWeek_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            SqlGenerator.DbFunction_DATEPART(generator, "w", exp.Expression);
        }
    }
}
