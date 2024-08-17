﻿using Chloe.DbExpressions;
using Chloe.RDBMS;
using Chloe.RDBMS.PropertyHandlers;

namespace Chloe.SQLite.PropertyHandlers
{
    class Now_Handler : Now_HandlerBase
    {
        public override void Process(DbMemberAccessExpression exp, SqlGeneratorBase generator)
        {
            generator.SqlBuilder.Append("DATETIME('NOW','LOCALTIME')");
        }
    }
}
