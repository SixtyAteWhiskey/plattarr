using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Upstream
{
    // The rows one stage plans for one table, kept with the source row each came from. Writing
    // inserts them on the session's connection and transaction (the repositories' caller-
    // transaction overload, never a plain InsertMany, which commits at once), records every new
    // ID in the IdMap and counts each source row as imported, so the reconciliation balances.
    public class RowBatch<TModel>
        where TModel : ModelBase, new()
    {
        private readonly List<TModel> _models = new List<TModel>();
        private readonly List<long> _sourceIds = new List<long>();

        public int Count => _models.Count;

        public IReadOnlyList<TModel> Models => _models;

        public void Add(long sourceId, TModel model)
        {
            _sourceIds.Add(sourceId);
            _models.Add(model);
        }

        public void Write(ImportSession session, string app, string table, IBasicRepository<TModel> repository)
        {
            repository.InsertMany(_models, session.Connection, session.Transaction);

            for (var i = 0; i < _models.Count; i++)
            {
                session.IdMap.Record(app, table, _sourceIds[i], _models[i].Id);
                session.Imported(app, table);
            }
        }
    }
}
