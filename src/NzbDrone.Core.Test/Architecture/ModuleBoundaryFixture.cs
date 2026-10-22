using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Test.Architecture
{
    // Boundary rules 2 and 3 (docs/module-interface.md): core code never references a module type,
    // and a module never references another module. Core types that reference module code today are
    // listed in ModuleBoundaryAllowlist. The list may only shrink: a new core-to-module reference
    // fails, and so does an entry that no longer references a module.
    [TestFixture]
    public class ModuleBoundaryFixture
    {
        private static readonly Lazy<Dictionary<Type, HashSet<Type>>> References =
            new(() => TypeReferenceScanner.Scan(typeof(MediaKind).Assembly));

        private static List<string> CoreTypesReferencingModules()
        {
            return References.Value
                .Where(x => ModuleBoundary.ModuleOf(x.Key) == null)
                .Where(x => x.Value.Any(r => ModuleBoundary.ModuleOf(r) != null))
                .Select(x => x.Key.FullName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        [Test]
        public void core_should_not_reference_modules_outside_the_allowlist()
        {
            var actual = CoreTypesReferencingModules();
            var allowed = ModuleBoundaryAllowlist.Types;

            var report = Environment.GetEnvironmentVariable("PLATTARR_BOUNDARY_REPORT");

            if (!string.IsNullOrEmpty(report))
            {
                File.WriteAllLines(report, actual.Select(t =>
                    t + " -> " + string.Join(", ", References.Value.Single(x => x.Key.FullName == t).Value
                        .Where(r => ModuleBoundary.ModuleOf(r) != null)
                        .Select(r => r.FullName)
                        .OrderBy(r => r, StringComparer.Ordinal))));
            }

            var added = actual.Except(allowed).ToList();
            var stale = allowed.Except(actual).ToList();

            added.Should().BeEmpty("core code must not start referencing module code (boundary rule 2); generalize it or move it into the module");
            stale.Should().BeEmpty("these types no longer reference module code; remove them from ModuleBoundaryAllowlist");
        }

        [Test]
        public void allowlist_should_have_no_duplicates()
        {
            var types = ModuleBoundaryAllowlist.Types;

            types.Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void modules_should_not_reference_other_modules()
        {
            var violations = References.Value
                .Select(x => new { Type = x.Key, Kind = ModuleBoundary.ModuleOf(x.Key) })
                .Where(x => x.Kind != null)
                .SelectMany(x => References.Value[x.Type]
                    .Where(r => ModuleBoundary.ModuleOf(r) is { } other && other != x.Kind)
                    .Select(r => $"{x.Type.FullName} -> {r.FullName}"))
                .ToList();

            violations.Should().BeEmpty("a module never references another module (boundary rule 3)");
        }

        [Test]
        public void scanner_should_find_references_only_inside_lambdas_and_async_methods()
        {
            var references = TypeReferenceScanner.Scan(typeof(ScannerSample).Assembly)[typeof(ScannerSample)];

            references.Should().Contain(typeof(ScannerLambdaTarget));
            references.Should().Contain(typeof(ScannerAsyncTarget));
        }
    }

    internal class ScannerLambdaTarget
    {
    }

    internal class ScannerAsyncTarget
    {
    }

    internal class ScannerSample
    {
        public Func<object> Lambda()
        {
            return () => new ScannerLambdaTarget();
        }

        public async System.Threading.Tasks.Task<string> Async()
        {
            await System.Threading.Tasks.Task.Yield();

            return new ScannerAsyncTarget().ToString();
        }
    }
}
