using System;
using System.Collections.Generic;
using System.Data;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    // The upstream importer writes every table on one connection and one transaction, so a failed
    // run leaves nothing behind. The repositories' own InsertMany commits on its own connection
    // and cannot do that; these overloads take the caller's connection and transaction.
    [TestFixture]
    public class CallerTransactionRepositoryFixture : DbTest<BasicRepository<Tag>, Tag>
    {
        private static List<Tag> NewTags(params string[] labels)
        {
            var tags = new List<Tag>();

            foreach (var label in labels)
            {
                tags.Add(new Tag { Label = label });
            }

            return tags;
        }

        private IDatabase Database => Mocker.Resolve<IDatabase>();

        [Test]
        public void insert_many_on_a_caller_transaction_should_assign_ids_and_keep_rows_after_commit()
        {
            var tags = NewTags("a", "b");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                Subject.InsertMany(tags, connection, transaction);
                transaction.Commit();
            }

            tags.Should().OnlyContain(t => t.Id > 0);
            tags[0].Id.Should().NotBe(tags[1].Id);
            AllStoredModels.Should().HaveCount(2);
        }

        [Test]
        public void insert_many_on_a_caller_transaction_should_leave_nothing_after_rollback()
        {
            var tags = NewTags("a", "b");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                Subject.InsertMany(tags, connection, transaction);
                transaction.Rollback();
            }

            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void rows_written_by_two_repositories_on_one_transaction_should_roll_back_together()
        {
            var secondRepository = Mocker.Resolve<BasicRepository<DelayProfile>>();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                Subject.InsertMany(NewTags("a"), connection, transaction);

                var profiles = new List<DelayProfile>
                {
                    new DelayProfile { Order = 1, Tags = new HashSet<int>() }
                };

                secondRepository.InsertMany(profiles, connection, transaction);
                profiles[0].Id.Should().BeGreaterThan(0);

                transaction.Rollback();
            }

            AllStoredModels.Should().BeEmpty();
            secondRepository.All().Should().NotContain(p => p.Order == 1);
        }

        [Test]
        public void insert_many_on_a_caller_transaction_should_refuse_a_model_that_has_an_id()
        {
            var tags = NewTags("a");
            tags[0].Id = 5;

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                Action insert = () => Subject.InsertMany(tags, connection, transaction);

                insert.Should().Throw<InvalidOperationException>();
                transaction.Rollback();
            }
        }

        [Test]
        public void update_many_on_a_caller_transaction_should_apply_after_commit_and_not_after_rollback()
        {
            var tags = NewTags("a", "b");
            Subject.InsertMany(tags);

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                tags[0].Label = "rolled-back";
                Subject.UpdateMany(new List<Tag> { tags[0] }, connection, transaction);
                transaction.Rollback();
            }

            Subject.Get(tags[0].Id).Label.Should().Be("a");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                tags[0].Label = "committed";
                Subject.UpdateMany(new List<Tag> { tags[0] }, connection, transaction);
                transaction.Commit();
            }

            Subject.Get(tags[0].Id).Label.Should().Be("committed");
        }

        [Test]
        public void update_many_on_a_caller_transaction_should_refuse_a_model_without_an_id()
        {
            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                Action update = () => Subject.UpdateMany(NewTags("a"), connection, transaction);

                update.Should().Throw<InvalidOperationException>();
                transaction.Rollback();
            }
        }
    }
}
