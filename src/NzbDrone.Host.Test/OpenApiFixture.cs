using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NzbDrone.Host;
using NzbDrone.Test.Common;
using Radarr.Api.V3.System;
using Radarr.Http.Frontend;
using Swashbuckle.AspNetCore.Swagger;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class OpenApiFixture : TestBase
    {
        [Test]
        public void should_generate_the_v3_document_for_every_controller()
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddRouting(options => options.LowercaseUrls = true);

            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(SystemController).Assembly)
                .AddApplicationPart(typeof(StaticResourceController).Assembly);

            builder.Services.AddSwaggerGen(OpenApiConfiguration.Configure);

            using var app = builder.Build();

            var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v3");

            document.Paths.Keys.Should().Contain("/api/v3/movie");
            document.Paths.Keys.Should().Contain("/api/v3/series");
            document.Paths.Keys.Should().Contain("/api/v3/release/tv/push");

            document.Components.Schemas.Keys.Should().Contain("MonitorTypes");
            document.Components.Schemas.Keys.Should().Contain("TvMonitorTypes");
            document.Components.Schemas.Keys.Should().Contain("MusicMonitorTypes");
        }

        [Test]
        public void should_keep_the_movie_schema_ids_and_prefix_only_the_tv_core_types_that_share_a_name()
        {
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Movies.MonitorTypes)).Should().Be("MonitorTypes");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Tv.MonitorTypes)).Should().Be("TvMonitorTypes");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Tv.Series)).Should().Be("Series");
        }

        [Test]
        public void should_prefix_only_the_music_core_types_that_share_a_name_and_not_move_the_tv_ids()
        {
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Music.MonitorTypes)).Should().Be("MusicMonitorTypes");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Music.Artist)).Should().Be("Artist");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Tv.MonitorTypes)).Should().Be("TvMonitorTypes");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Music.NewItemMonitorTypes)).Should().Be("MusicNewItemMonitorTypes");
            OpenApiSchemaIds.Get(typeof(NzbDrone.Core.Tv.NewItemMonitorTypes)).Should().Be("NewItemMonitorTypes");
        }
    }
}
