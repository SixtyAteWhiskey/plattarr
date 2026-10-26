using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Hygiene;
using Radarr.Http;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Hygiene
{
    [V3ApiController("hygiene/rule")]
    public class HygieneRuleController : Controller
    {
        private readonly IHygieneRuleService _rules;

        public HygieneRuleController(IHygieneRuleService rules)
        {
            _rules = rules;
        }

        [HttpGet]
        public Ok<List<HygieneRule>> GetRules()
        {
            return TypedResults.Ok(_rules.All());
        }

        [HttpPost]
        public Ok<HygieneRule> Create([FromBody] HygieneRule rule)
        {
            if (rule == null)
            {
                throw new BadRequestException("A Hygiene rule is required.");
            }

            rule.Id = 0;
            return TypedResults.Ok(Save(rule));
        }

        [HttpPut("{id:int}")]
        public Ok<HygieneRule> Update(int id, [FromBody] HygieneRule rule)
        {
            if (rule == null)
            {
                throw new BadRequestException("A Hygiene rule is required.");
            }

            if (_rules.Find(id) == null)
            {
                throw new NotFoundException();
            }

            rule.Id = id;
            return TypedResults.Ok(Save(rule));
        }

        [HttpDelete("{id:int}")]
        public Ok<object> Delete(int id)
        {
            if (_rules.Find(id) == null)
            {
                throw new NotFoundException();
            }

            _rules.Delete(id);
            return TypedResults.Ok<object>(new { });
        }

        private HygieneRule Save(HygieneRule rule)
        {
            try
            {
                return _rules.Upsert(rule);
            }
            catch (ArgumentException ex)
            {
                throw new BadRequestException(ex.Message);
            }
        }
    }
}
