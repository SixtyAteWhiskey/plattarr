using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.MediaCover
{
    public static class MediaCoverBackfill
    {
        public static CoverBackfillResult Run<T>(IEnumerable<T> items,
                                                  Func<T, int> id,
                                                  Func<T, IEnumerable<MediaCover>> images,
                                                  Func<int, MediaCoverTypes, int?, string> path,
                                                  Action<T> ensure,
                                                  IDiskProvider diskProvider,
                                                  Logger logger,
                                                  string kind)
        {
            var result = new CoverBackfillResult();

            foreach (var item in items)
            {
                var covers = (images(item) ?? Array.Empty<MediaCover>())
                    .Where(c => c.CoverType != MediaCoverTypes.Unknown)
                    .GroupBy(c => c.CoverType)
                    .Select(g => g.First())
                    .ToList();

                bool Exists(MediaCover cover, int? height)
                {
                    var file = path(id(item), cover.CoverType, height);
                    return diskProvider.FileExists(file) && diskProvider.GetFileSize(file) > 0;
                }

                bool Complete(MediaCover cover)
                {
                    return Exists(cover, null) && RequiredHeights(cover.CoverType).All(h => Exists(cover, h));
                }

                if (covers.Any(c => !Complete(c)))
                {
                    try
                    {
                        ensure(item);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, "Could not backfill covers for {0} ID {1}", kind, id(item));
                    }
                }

                result.Checked += covers.Count;
                result.Missing += covers.Count(c => !Complete(c));
            }

            return result;
        }

        private static IEnumerable<int> RequiredHeights(MediaCoverTypes type)
        {
            return type switch
            {
                MediaCoverTypes.Poster or MediaCoverTypes.Headshot => new[] { 500, 250 },
                MediaCoverTypes.Banner => new[] { 70, 35 },
                MediaCoverTypes.Fanart or MediaCoverTypes.Screenshot => new[] { 360, 180 },
                _ => Array.Empty<int>()
            };
        }
    }
}
