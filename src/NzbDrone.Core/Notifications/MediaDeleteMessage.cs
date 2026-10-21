using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Notifications
{
    public class MediaDeleteMessage
    {
        public string Message { get; set; }
        public ActivitySubject Subject { get; set; }
        public IMediaNotificationData Data { get; set; }
        public bool DeletedFiles { get; set; }
        public string DeletedFilesMessage { get; set; }

        public override string ToString()
        {
            return Message;
        }

        public MediaDeleteMessage(ActivitySubject subject, IMediaNotificationData data, bool deleteFiles)
        {
            Subject = subject;
            Data = data;
            DeletedFiles = deleteFiles;
            DeletedFilesMessage = DeletedFiles ?
                $"{subject.KindLabel} removed and all files were deleted" :
                $"{subject.KindLabel} removed, files were not deleted";
            Message = subject.ParentTitle + " - " + DeletedFilesMessage;
        }
    }
}
