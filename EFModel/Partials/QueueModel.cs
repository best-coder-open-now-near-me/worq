using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFModel
{
    public partial class QueueEntities
    {

        public virtual ObjectResult<Type> LoadList(string user, string type)
        {
            var userParam = new ObjectParameter("user", user);
            var typeParam = new ObjectParameter("type", type);

            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<Type>("q_Load_List", userParam, typeParam);
        }

        public virtual ObjectResult<q_Load_List_Result> LoadList(string user, string type, DateTime date1, DateTime date2)
        {

            var userParam = new ObjectParameter("user", user);
            var typeParam = new ObjectParameter("type", type);
            var date1Param = new ObjectParameter("date1", date1);
            var date2Param = new ObjectParameter("date2", date2);

            return ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction<q_Load_List_Result>("q_Load_List", userParam, typeParam, date1Param, date2Param);
        }

    }
}
