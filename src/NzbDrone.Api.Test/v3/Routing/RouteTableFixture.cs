using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.Series;

namespace NzbDrone.Api.Test.v3.Routing
{
    // Two actions on the same verb and route make ASP.NET throw an AmbiguousMatchException when the
    // route is hit (or shadow one another), and nothing at build time says so. Movies and TV share
    // one app, so every Sonarr route that Radarr already serves has to move beside it (/tv suffix).
    // This walks every action of every API controller, the way MVC discovers them, and fails on a
    // verb and route that two actions claim. A parameter segment counts as a wildcard; a literal
    // segment beats a parameter segment in MVC, so those pairs are fine.
    [TestFixture]
    public class RouteTableFixture : TestBase
    {
        [Test]
        public void no_two_actions_should_claim_the_same_verb_and_route()
        {
            var claims = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var controller in typeof(SeriesController).Assembly.GetTypes().Where(IsController))
            {
                var controllerTemplate = controller.GetCustomAttributes(true).OfType<IRouteTemplateProvider>()
                    .Select(a => a.Template).FirstOrDefault(t => t != null);

                if (controllerTemplate == null)
                {
                    continue;
                }

                var root = controllerTemplate.Replace("[controller]", controller.Name.Replace("Controller", string.Empty));

                foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (action.GetCustomAttributes(true).OfType<NonActionAttribute>().Any())
                    {
                        continue;
                    }

                    foreach (var http in action.GetCustomAttributes(true).OfType<HttpMethodAttribute>())
                    {
                        var path = string.IsNullOrEmpty(http.Template) ? root : $"{root}/{http.Template}";
                        var key = $"{string.Join("|", http.HttpMethods)} {Normalize(path)}";

                        if (!claims.TryGetValue(key, out var owners))
                        {
                            claims[key] = owners = new List<string>();
                        }

                        owners.Add($"{controller.Name}.{action.Name}");
                    }
                }
            }

            var clashes = claims.Where(c => c.Value.Distinct().Count() > 1)
                .Select(c => $"{c.Key}: {string.Join(", ", c.Value.Distinct())}")
                .ToList();

            clashes.Should().BeEmpty();
        }

        [Test]
        public void the_tv_routes_should_sit_beside_the_movie_routes()
        {
            var routes = typeof(SeriesController).Assembly.GetTypes().Where(IsController)
                .SelectMany(c => c.GetCustomAttributes(true).OfType<IRouteTemplateProvider>().Select(a => a.Template))
                .Where(t => t != null)
                .ToList();

            routes.Should().Contain(new[]
            {
                "api/v3/wanted/missing/tv",
                "api/v3/wanted/cutoff/tv",
                "api/v3/calendar/tv",
                "api/v3/blocklist/tv",
                "api/v3/parse/tv",
                "api/v3/history/tv",
                "api/v3/manualimport/tv",
                "api/v3/extrafile/tv",
                "api/v3/autotagging/tv"
            });
        }

        private static bool IsController(Type type)
        {
            return type.IsClass && !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type);
        }

        private static string Normalize(string path)
        {
            return Regex.Replace(path.Trim('/'), @"\{[^}]*\}", "{}");
        }
    }
}
