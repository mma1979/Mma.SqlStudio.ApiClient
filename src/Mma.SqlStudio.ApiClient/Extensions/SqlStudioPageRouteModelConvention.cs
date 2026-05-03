using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;
using System.Linq;

namespace Mma.SqlStudio.ApiClient.Extensions
{
    public class SqlStudioPageRouteModelConvention : IPageRouteModelConvention
    {
        private readonly string _route;

        public SqlStudioPageRouteModelConvention(string route)
        {
            _route = route?.Trim('/') ?? "sql-studio";
        }

        public void Apply(PageRouteModel model)
        {
            // The Razor Page is usually at /Pages/SqlStudio.cshtml
            // When compiled in an RCL, we check the relative path
            if (model.RelativePath.Contains("/Pages/SqlStudio.cshtml", StringComparison.OrdinalIgnoreCase))
            {
                model.Selectors.Clear();
                model.Selectors.Add(new SelectorModel
                {
                    AttributeRouteModel = new AttributeRouteModel
                    {
                        Template = _route
                    }
                });
            }
        }
    }
}
