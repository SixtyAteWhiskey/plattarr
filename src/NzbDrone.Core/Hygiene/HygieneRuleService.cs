using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Hygiene
{
    public interface IHygieneRuleService
    {
        List<HygieneRule> All();
        HygieneRule Find(int id);
        HygieneRule Upsert(HygieneRule rule);
        void Delete(int id);
        HygieneRule Match(IEnumerable<HygieneRule> rules, string reasonCode, int mediaKind, int downloadClientId);
    }

    public class HygieneRuleService : IHygieneRuleService
    {
        private static readonly HashSet<string> SupportedReasons = new(StringComparer.Ordinal)
        {
            "client-failed",
            "import-blocked",
            "download-warning",
            "download-stalled",
            "download-metadata",
            "download-slow",
            "download-unstable"
        };

        private readonly IHygieneRuleRepository _repository;
        private readonly Logger _logger;

        public HygieneRuleService(IHygieneRuleRepository repository, Logger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public List<HygieneRule> All()
        {
            return _repository.All().ToList();
        }

        public HygieneRule Find(int id)
        {
            return _repository.Find(id);
        }

        public HygieneRule Upsert(HygieneRule rule)
        {
            if (rule == null || !SupportedReasons.Contains(rule.ReasonCode) ||
                (rule.MediaKind != 0 && !Enum.IsDefined((MediaKind)rule.MediaKind)) ||
                rule.DownloadClientId < 0 || rule.StrikeLimit < 3 || rule.StrikeLimit > 100 ||
                rule.StrikeIntervalMinutes < 1 || rule.StrikeIntervalMinutes > 1440 ||
                (rule.ReasonCode == "download-warning" && rule.DeleteFilesOnFailure) ||
                (rule.ReasonCode == "download-slow" && (rule.MinimumDownloadRateKiBps < 1 || rule.MinimumDownloadRateKiBps > 1048576)) ||
                (rule.ReasonCode != "download-slow" && rule.MinimumDownloadRateKiBps != 0))
            {
                throw new ArgumentException("Invalid Hygiene rule scope, reason, or strike threshold.");
            }

            var isNewRule = rule.Id == 0;
            var existingRule = isNewRule ? null : _repository.Find(rule.Id);

            if (_repository.All().Any(x => x.Id != rule.Id && x.ReasonCode == rule.ReasonCode &&
                                           x.MediaKind == rule.MediaKind && x.DownloadClientId == rule.DownloadClientId))
            {
                throw new ArgumentException("A Hygiene rule already exists for this reason, media kind, and client.");
            }

            var savedRule = isNewRule ? _repository.Insert(rule) : _repository.Update(rule);

            if (isNewRule)
            {
                _logger.Info("Hygiene rule {0} (reason: {1}, media kind: {2}, download client id: {3}) was created with monitoring {4}, source file deletion {5}, and replacement search {6}",
                    savedRule.Id,
                    savedRule.ReasonCode,
                    GetMediaKindName(savedRule.MediaKind),
                    savedRule.DownloadClientId,
                    savedRule.Enabled ? "enabled" : "disabled",
                    savedRule.DeleteFilesOnFailure ? "enabled" : "disabled",
                    savedRule.SearchForReplacement ? "enabled" : "disabled");
            }
            else if (existingRule != null && existingRule.Enabled != savedRule.Enabled)
            {
                _logger.Info("Hygiene rule {0} (reason: {1}, media kind: {2}, download client id: {3}) monitoring changed from {4} to {5}",
                    savedRule.Id,
                    savedRule.ReasonCode,
                    GetMediaKindName(savedRule.MediaKind),
                    savedRule.DownloadClientId,
                    existingRule.Enabled ? "enabled" : "disabled",
                    savedRule.Enabled ? "enabled" : "disabled");
            }

            if (existingRule != null && existingRule.DeleteFilesOnFailure != savedRule.DeleteFilesOnFailure)
            {
                _logger.Info("Hygiene rule {0} (reason: {1}, media kind: {2}, download client id: {3}) source file deletion changed from {4} to {5}",
                    savedRule.Id,
                    savedRule.ReasonCode,
                    GetMediaKindName(savedRule.MediaKind),
                    savedRule.DownloadClientId,
                    existingRule.DeleteFilesOnFailure ? "enabled" : "disabled",
                    savedRule.DeleteFilesOnFailure ? "enabled" : "disabled");
            }

            if (existingRule != null && existingRule.SearchForReplacement != savedRule.SearchForReplacement)
            {
                _logger.Info("Hygiene rule {0} (reason: {1}, media kind: {2}, download client id: {3}) replacement search changed from {4} to {5}",
                    savedRule.Id,
                    savedRule.ReasonCode,
                    GetMediaKindName(savedRule.MediaKind),
                    savedRule.DownloadClientId,
                    existingRule.SearchForReplacement ? "enabled" : "disabled",
                    savedRule.SearchForReplacement ? "enabled" : "disabled");
            }

            return savedRule;
        }

        private static string GetMediaKindName(int mediaKind)
        {
            return mediaKind == 0 ? "all media" : ((MediaKind)mediaKind).ToString();
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
        }

        public HygieneRule Match(IEnumerable<HygieneRule> rules, string reasonCode, int mediaKind, int downloadClientId)
        {
            return rules.Where(x => x.ReasonCode == reasonCode &&
                                    (x.MediaKind == 0 || x.MediaKind == mediaKind) &&
                                    (x.DownloadClientId == 0 || x.DownloadClientId == downloadClientId))
                        .OrderByDescending(x => (x.MediaKind == mediaKind && mediaKind != 0 ? 2 : 0) +
                                                (x.DownloadClientId == downloadClientId && downloadClientId != 0 ? 1 : 0))
                        .FirstOrDefault();
        }
    }
}
