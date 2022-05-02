using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{

    public class DispatchStore: Store<Dispatch>, IDispatchStore
    {
        public DispatchStore(TopoMojoDbContext dbContext)
        :base(dbContext)
        {

        }

        // If entity has searchable fields, use this:
        // public override IQueryable<Dispatch> List(string term = null)
        // {
        //     var q = base.List();

        //     if (!string.IsNullOrEmpty(term))
        //     {
        //         term = term.ToLower();

        //         q = q.Where(t =>
        //             t.Name.ToLower().Contains(term)
        //         );
        //     }

        //     return q;
        // }

    }
}
