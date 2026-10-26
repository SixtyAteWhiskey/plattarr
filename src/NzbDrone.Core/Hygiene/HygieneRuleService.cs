using System;
using System.Collections.Generic;
using System.Linq;
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
            "download-slow"
        };

        private readonly IHygieneRuleRepository _repository;

        public HygieneRuleService(IHygieneRuleRepository repository)
        {
            _repository = repository;
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
                rule.DownloadClientId < 0 || rule.StrikeLimit < 1 || rule.StrikeLimit > 100 ||
                rule.StrikeIntervalMinutes < 1 || rule.StrikeIntervalMinutes > 1440 ||
                (rule.ReasonCode == "download-slow" && (rule.MinimumDownloadRateKiBps < 1 || rule.MinimumDownloadRateKiBps > 1048576)) ||
                (rule.ReasonCode != "download-slow" && rule.MinimumDownloadRateKiBps != 0))
            {
                throw new ArgumentException("Invalid Hygiene rule scope, reason, or strike threshold.");
            }

            if (_repository.All().Any(x => x.Id != rule.Id && x.ReasonCode == rule.ReasonCode &&
                                           x.MediaKind == rule.MediaKind && x.DownloadClientId == rule.DownloadClientId))
            {
                throw new ArgumentException("A Hygiene rule already exists for this reason, media kind, and client.");
            }

            return rule.Id == 0 ? _repository.Insert(rule) : _repository.Update(rule);
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
