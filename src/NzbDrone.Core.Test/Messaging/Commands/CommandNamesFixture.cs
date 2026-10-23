using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Test.Messaging.Commands
{
    [TestFixture]
    public class CommandNamesFixture
    {
        // The command queue and the API look a command up by its simple type name (Command.Name is the type
        // name without "Command"), so two modules can't both define, say, RenameFilesCommand.
        [Test]
        public void every_command_should_have_a_unique_name()
        {
            var commands = typeof(Command).Assembly.GetTypes()
                .Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            var duplicates = commands
                .GroupBy(t => t.Name.Replace("Command", string.Empty), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.FullName))}")
                .ToList();

            duplicates.Should().BeEmpty();
        }
    }
}
