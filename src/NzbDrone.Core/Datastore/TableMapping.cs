using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFilters;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.IndexerHistory;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.IndexerVersions;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Tags;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Update.History;
using static Dapper.SqlMapper;

namespace NzbDrone.Core.Datastore
{
    public static class TableMapping
    {
        static TableMapping()
        {
            Mapper = new TableMapper();
        }

        public static TableMapper Mapper { get; private set; }

        public static void Map()
        {
            var modules = typeof(IModuleTableMap).Assembly.ImplementationsOf<IModuleTableMap>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => (IModuleTableMap)Activator.CreateInstance(t))
                .OrderBy(m => m.Kind)
                .ToList();

            RegisterMappers();

            foreach (var module in modules)
            {
                module.RegisterTypeHandlers();
                Mapper.ModuleTablePrefixes.Add(module.TablePrefix);
            }

            Mapper.Entity<Config>("Config").RegisterModel();

            Mapper.Entity<ScheduledTask>("ScheduledTasks").RegisterModel()
                  .Ignore(i => i.Priority);

            Mapper.Entity<NotificationDefinition>("Notifications").RegisterModel()
                  .Ignore(x => x.ImplementationName)
                  .Ignore(i => i.SupportsOnGrab)
                  .Ignore(i => i.SupportsOnDownload)
                  .Ignore(i => i.SupportsOnUpgrade)
                  .Ignore(i => i.SupportsOnRename)
                  .Ignore(i => i.SupportsOnMovieAdded)
                  .Ignore(i => i.SupportsOnMovieDelete)
                  .Ignore(i => i.SupportsOnMovieFileDelete)
                  .Ignore(i => i.SupportsOnMovieFileDeleteForUpgrade)
                  .Ignore(i => i.SupportsOnHealthIssue)
                  .Ignore(i => i.SupportsOnHealthRestored)
                  .Ignore(i => i.SupportsOnApplicationUpdate)
                  .Ignore(i => i.SupportsOnManualInteractionRequired);

            Mapper.Entity<DownloadClientDefinition>("DownloadClients").RegisterModel()
                  .Ignore(x => x.ImplementationName)
                  .Ignore(d => d.Protocol);

            Mapper.Entity<QualityDefinition>("QualityDefinitions").RegisterModel()
                  .Ignore(d => d.GroupName)
                  .Ignore(d => d.Weight);

            Mapper.Entity<CustomFormat>("CustomFormats").RegisterModel();

            Mapper.Entity<QualityProfile>("QualityProfiles").RegisterModel();
            Mapper.Entity<Log>("Logs").RegisterModel();

            // RemoteMovie is not persisted. It has no type handler, so RegisterModel skips it.
            Mapper.Entity<PendingRelease>("PendingReleases").RegisterModel();

            Mapper.Entity<RemotePathMapping>("RemotePathMappings").RegisterModel();
            Mapper.Entity<Tag>("Tags").RegisterModel();
            Mapper.Entity<ReleaseProfile>("ReleaseProfiles").RegisterModel();

            Mapper.Entity<DelayProfile>("DelayProfiles").RegisterModel();
            Mapper.Entity<User>("Users").RegisterModel();
            Mapper.Entity<CommandModel>("Commands").RegisterModel()
                  .Ignore(c => c.Message);

            Mapper.Entity<DownloadClientStatus>("DownloadClientStatus").RegisterModel();
            Mapper.Entity<NotificationStatus>("NotificationStatus").RegisterModel();

            Mapper.Entity<CustomFilter>("CustomFilters").RegisterModel();

            Mapper.Entity<DownloadHistory>("DownloadHistory").RegisterModel();

            Mapper.Entity<UpdateHistory>("UpdateHistory").RegisterModel();

            // Prowlarr's indexer layer.
            Mapper.Entity<Indexers.IndexerDefinition>("Indexers").RegisterModel()
                  .Ignore(x => x.ImplementationName)
                  .Ignore(i => i.Description)
                  .Ignore(i => i.Language)
                  .Ignore(i => i.Encoding)
                  .Ignore(i => i.IndexerUrls)
                  .Ignore(i => i.LegacyUrls)
                  .Ignore(i => i.Protocol)
                  .Ignore(i => i.Privacy)
                  .Ignore(i => i.SupportsRss)
                  .Ignore(i => i.SupportsSearch)
                  .Ignore(i => i.SupportsRedirect)
                  .Ignore(i => i.SupportsPagination)
                  .Ignore(i => i.Capabilities);

            Mapper.Entity<Indexers.IndexerStatus>("IndexerStatus").RegisterModel();

            Mapper.Entity<IndexerProxyDefinition>("IndexerProxies").RegisterModel()
                  .Ignore(x => x.ImplementationName);

            Mapper.Entity<IndexerHistoryItem>("IndexerHistory").RegisterModel();

            Mapper.Entity<IndexerDefinitionVersion>("IndexerDefinitionVersions").RegisterModel();

            Mapper.Entity<Indexers.Overrides.IndexerOverride>("IndexerOverrides").RegisterModel();

            foreach (var module in modules)
            {
                module.MapTables(Mapper);
            }
        }

        private static void RegisterMappers()
        {
            RegisterEmbeddedConverter();
            RegisterProviderSettingConverter();

            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.AddTypeHandler(new DapperUtcConverter());
            SqlMapper.AddTypeHandler(new DapperQualityIntConverter());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<QualityProfileQualityItem>>(new QualityIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<ProfileFormatItem>>(new CustomFormatIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<ICustomFormatSpecification>>(new CustomFormatSpecificationListConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<QualityModel>(new QualityIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<Dictionary<string, string>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<IDictionary<string, string>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<int>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<MediaKind>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<KeyValuePair<string, int>>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<KeyValuePair<string, int>>());
            SqlMapper.AddTypeHandler(new DapperLanguageIntConverter());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<Language>>(new LanguageIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<string>>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<ReleaseInfo>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<PendingReleaseAdditionalInfo>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<HashSet<int>>());
            SqlMapper.AddTypeHandler(new OsPathConverter());
            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));
            SqlMapper.AddTypeHandler(new GuidConverter());
            SqlMapper.RemoveTypeMap(typeof(TimeSpan));
            SqlMapper.RemoveTypeMap(typeof(TimeSpan?));
            SqlMapper.AddTypeHandler(new TimeSpanConverter());
            SqlMapper.AddTypeHandler(new CommandConverter());
            SqlMapper.AddTypeHandler(new SystemVersionConverter());
        }

        private static void RegisterProviderSettingConverter()
        {
            var settingTypes = typeof(IProviderConfig).Assembly.ImplementationsOf<IProviderConfig>()
                .Where(x => !x.ContainsGenericParameters);

            var providerSettingConverter = new ProviderSettingConverter();
            foreach (var embeddedType in settingTypes)
            {
                SqlMapper.AddTypeHandler(embeddedType, providerSettingConverter);
            }
        }

        private static void RegisterEmbeddedConverter()
        {
            var embeddedTypes = typeof(IEmbeddedDocument).Assembly.ImplementationsOf<IEmbeddedDocument>();

            var embeddedConverterDefinition = typeof(EmbeddedDocumentConverter<>).GetGenericTypeDefinition();
            var genericListDefinition = typeof(List<>).GetGenericTypeDefinition();

            foreach (var embeddedType in embeddedTypes)
            {
                var embeddedListType = genericListDefinition.MakeGenericType(embeddedType);

                RegisterEmbeddedConverter(embeddedType, embeddedConverterDefinition);
                RegisterEmbeddedConverter(embeddedListType, embeddedConverterDefinition);
            }
        }

        private static void RegisterEmbeddedConverter(Type embeddedType, Type embeddedConverterDefinition)
        {
            var embeddedConverterType = embeddedConverterDefinition.MakeGenericType(embeddedType);
            var converter = (ITypeHandler)Activator.CreateInstance(embeddedConverterType);

            SqlMapper.AddTypeHandler(embeddedType, converter);
        }
    }
}
