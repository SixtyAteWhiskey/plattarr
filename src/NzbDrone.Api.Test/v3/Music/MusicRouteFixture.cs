using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NUnit.Framework;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Artist;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicRouteFixture : TestBase
    {
        [Test]
        public void the_music_routes_that_would_collide_should_sit_beside_the_video_routes()
        {
            var routes = typeof(ArtistController).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
                .SelectMany(c => c.GetCustomAttributes(true).OfType<IRouteTemplateProvider>().Select(a => a.Template))
                .Where(t => t != null)
                .ToList();

            routes.Should().Contain(new[]
            {
                "api/v3/music/rootfolder",
                "api/v3/music/config/naming",
                "api/v3/music/qualityprofile",
                "api/v3/music/qualityprofile/schema",
                "api/v3/music/qualitydefinition",
                "api/v3/artist/rename",
                "api/v3/artist/retag",
                "api/v3/artist/editor",
                "api/v3/artist/lookup",
                "api/v3/album/lookup",
                "api/v3/metadataprofile/schema"
            });
        }

        [Test]
        public void the_library_controllers_should_take_their_names_from_the_class()
        {
            // [V3ApiController] with no route takes "api/v3/[controller]": these are the names the UI calls.
            var names = new[]
            {
                typeof(Radarr.Api.V3.Music.Artist.ArtistController),
                typeof(Radarr.Api.V3.Music.Albums.AlbumController),
                typeof(Radarr.Api.V3.Music.Tracks.TrackController),
                typeof(Radarr.Api.V3.Music.TrackFiles.TrackFileController),
                typeof(Radarr.Api.V3.Music.AlbumStudio.AlbumStudioController),
                typeof(Radarr.Api.V3.Music.Profiles.Metadata.MetadataProfileController)
            };

            names.Select(t => t.GetCustomAttributes(true).OfType<IRouteTemplateProvider>().Select(a => a.Template).First(x => x != null))
                .Should().Equal("api/v3/[controller]", "api/v3/[controller]", "api/v3/[controller]", "api/v3/[controller]", "api/v3/[controller]", "api/v3/[controller]");
        }
    }
}
