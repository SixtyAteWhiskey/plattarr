using System;
using Dapper;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Datastore
{
    // One module's view of a shared activity table. Every query and delete built
    // from Builder() sees only this repository's media kind, and inserts refuse a
    // row of any other kind. Ids are unique across kinds, so Get and Delete by id
    // need no scope, but they get it anyway through Builder().
    public abstract class MediaKindRepository<TModel> : BasicRepository<TModel>
        where TModel : ModelBase, IMediaKindRecord, new()
    {
        protected MediaKindRepository(IDatabase database, IEventAggregator eventAggregator, MediaKind kind)
            : base(database, eventAggregator)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown media kind");
            }

            Kind = kind;
        }

        public MediaKind Kind { get; }

        // The table name is mapped and the kind is an integer, so nothing here comes
        // from user input.
        protected override SqlBuilder Builder() =>
            base.Builder().Where($"\"{_table}\".\"MediaKind\" = {(int)Kind}");

        public override int Count()
        {
            using var conn = _database.OpenConnection();

            return conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{_table}\" WHERE \"MediaKind\" = {(int)Kind}");
        }

        // Clears this kind's rows only; other media types keep theirs.
        public override void Purge(bool vacuum = false)
        {
            Delete(Builder());

            if (vacuum)
            {
                Vacuum();
            }
        }

        protected override void BeforeInsert(TModel model)
        {
            if (model.MediaKind != Kind)
            {
                throw new InvalidOperationException($"Can't insert a {typeof(TModel).Name} with MediaKind '{model.MediaKind}' through the {Kind} repository");
            }
        }
    }
}
