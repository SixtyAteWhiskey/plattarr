using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Modules
{
    // Contract 1, table half. TableMapping runs in DbFactory's static constructor, before the
    // container is built, so implementations are found by reflection and created with their
    // parameterless constructor. One per media type.
    //
    // A module maps every entity type it owns: its prefixed tables, and its own views of shared
    // tables (MovieHistory on History), which it reads through a MediaKindRepository.
    public interface IModuleTableMap
    {
        MediaKind Kind { get; }

        // "Movies_". Every table the module owns starts with it (plan section 3a).
        string TablePrefix { get; }

        // Dapper handlers for the module's embedded documents. Runs before any table is mapped.
        void RegisterTypeHandlers();

        // Runs after the core tables are mapped.
        void MapTables(TableMapper mapper);
    }
}
