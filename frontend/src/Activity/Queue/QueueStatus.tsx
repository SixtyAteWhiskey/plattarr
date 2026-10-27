import React from 'react';
import Icon, { IconKind, IconName } from 'Components/Icon';
import { StatusBadge, StatusTone } from 'Components/Status';
import Popover from 'Components/Tooltip/Popover';
import { icons, kinds } from 'Helpers/Props';
import { TooltipPosition } from 'Helpers/Props/tooltipPositions';
import {
  QueueTrackedDownloadState,
  QueueTrackedDownloadStatus,
  StatusMessage,
} from 'typings/Queue';
import translate from 'Utilities/String/translate';
import styles from './QueueStatus.css';

function getDetailedPopoverBody(statusMessages: StatusMessage[]) {
  return (
    <div>
      {statusMessages.map(({ title, messages }) => {
        return (
          <div
            key={title}
            className={messages.length ? undefined : styles.noMessages}
          >
            {title}
            <ul>
              {messages.map((message) => {
                return <li key={message}>{message}</li>;
              })}
            </ul>
          </div>
        );
      })}
    </div>
  );
}

interface QueueStatusProps {
  sourceTitle: string;
  status: string;
  trackedDownloadStatus?: QueueTrackedDownloadStatus;
  trackedDownloadState?: QueueTrackedDownloadState;
  statusMessages?: StatusMessage[];
  errorMessage?: string;
  position: TooltipPosition;
  canFlip?: boolean;
  display?: 'badge' | 'icon';
}

interface QueueStatusPresentation {
  badgeDetail?: string;
  badgeLabel: string;
  iconKind: IconKind;
  iconName: IconName;
  title: string;
  tone: StatusTone;
}

export function getQueueStatusPresentation({
  sourceTitle,
  status,
  trackedDownloadStatus = 'ok',
  trackedDownloadState = 'downloading',
  errorMessage,
}: Pick<
  QueueStatusProps,
  | 'sourceTitle'
  | 'status'
  | 'trackedDownloadStatus'
  | 'trackedDownloadState'
  | 'errorMessage'
>): QueueStatusPresentation {
  const hasWarning = trackedDownloadStatus === 'warning';
  const hasError = trackedDownloadStatus === 'error';

  // status === 'downloading'
  let iconName = icons.DOWNLOADING;
  let iconKind: IconKind = kinds.DEFAULT;
  let title = translate('Downloading');
  let badgeLabel = title;
  let badgeDetail: string | undefined = undefined;
  let tone: StatusTone = 'info';

  if (status === 'paused') {
    iconName = icons.PAUSED;
    title = translate('Paused');
    badgeLabel = title;
    tone = 'neutral';
  }

  if (status === 'queued') {
    iconName = icons.QUEUED;
    title = translate('Queued');
    badgeLabel = title;
  }

  if (status === 'completed') {
    iconName = icons.DOWNLOADED;
    title = translate('Downloaded');
    badgeLabel = title;
    tone = 'success';

    if (trackedDownloadState === 'importBlocked') {
      const detail = translate('UnableToImportAutomatically');

      title += ` - ${detail}`;
      iconKind = kinds.WARNING;
      badgeLabel = translate('Warning');
      badgeDetail = detail;
      tone = 'warning';
    }

    if (trackedDownloadState === 'importPending') {
      const detail = translate('WaitingToImport');

      title += ` - ${detail}`;
      iconKind = kinds.PURPLE;
      badgeLabel = translate('Pending');
      badgeDetail = detail;
      tone = 'info';
    }

    if (trackedDownloadState === 'importing') {
      const detail = translate('Importing');

      title += ` - ${detail}`;
      iconKind = kinds.PRIMARY;
      badgeLabel = detail;
      tone = 'info';
    }

    if (trackedDownloadState === 'failedPending') {
      const detail = translate('WaitingToProcess');

      title += ` - ${detail}`;
      iconKind = kinds.DANGER;
      badgeLabel = translate('Error');
      badgeDetail = detail;
      tone = 'danger';
    }
  }

  if (hasWarning) {
    iconKind = kinds.WARNING;
    badgeDetail = badgeDetail || badgeLabel;
    badgeLabel = translate('Warning');
    tone = 'warning';
  }

  if (status === 'delay') {
    iconName = icons.PENDING;
    title = translate('Pending');

    if (hasWarning) {
      badgeDetail = title;
    } else {
      badgeLabel = title;
      badgeDetail = undefined;
      tone = 'info';
    }
  }

  if (status === 'downloadClientUnavailable') {
    const detail = translate('DownloadClientUnavailable');

    iconName = icons.PENDING;
    iconKind = kinds.WARNING;
    title = translate('PendingDownloadClientUnavailable');
    badgeLabel = translate('Warning');
    badgeDetail = detail;
    tone = 'warning';
  }

  if (status === 'failed') {
    const detail = translate('DownloadFailed');

    iconName = icons.DOWNLOADING;
    iconKind = kinds.DANGER;
    title = detail;
    badgeLabel = translate('Error');
    badgeDetail = detail;
    tone = 'danger';
  }

  if (status === 'warning') {
    iconName = icons.DOWNLOADING;
    iconKind = kinds.WARNING;
    const warningMessage =
      errorMessage || translate('CheckDownloadClientForDetails');
    title = translate('DownloadWarning', { warningMessage });
    badgeLabel = translate('Warning');
    badgeDetail = undefined;
    tone = 'warning';
  }

  if (hasError) {
    iconKind = kinds.DANGER;
    badgeLabel = translate('Error');
    badgeDetail = undefined;
    tone = 'danger';

    if (status === 'completed') {
      iconName = icons.DOWNLOAD;
      title = translate('ImportFailed', { sourceTitle });
    } else {
      iconName = icons.DOWNLOADING;
      title = translate('DownloadFailed');
      badgeDetail = title;
    }
  }

  return {
    badgeDetail,
    badgeLabel,
    iconKind,
    iconName,
    title,
    tone,
  };
}

function getAccessibilityLabel(
  title: string,
  sourceTitle: string,
  statusMessages: StatusMessage[]
) {
  const details = statusMessages.flatMap(({ title, messages }) => [
    title,
    ...messages,
  ]);

  return [title, sourceTitle, ...details].filter(Boolean).join('. ');
}

function QueueStatus(props: QueueStatusProps) {
  const {
    sourceTitle,
    status,
    trackedDownloadStatus = 'ok',
    trackedDownloadState = 'downloading',
    statusMessages = [],
    errorMessage,
    position,
    canFlip = false,
    display = 'icon',
  } = props;

  const hasWarning = trackedDownloadStatus === 'warning';
  const hasError = trackedDownloadStatus === 'error';
  const presentation = getQueueStatusPresentation({
    sourceTitle,
    status,
    trackedDownloadStatus,
    trackedDownloadState,
    errorMessage,
  });
  const showBadge = display === 'badge';

  return (
    <Popover
      accessibilityLabel={getAccessibilityLabel(
        presentation.title,
        sourceTitle,
        statusMessages
      )}
      anchor={
        showBadge ? (
          <StatusBadge
            className={styles.badge}
            detail={presentation.badgeDetail}
            icon={presentation.iconName}
            label={presentation.badgeLabel}
            tone={presentation.tone}
          />
        ) : (
          <Icon
            name={presentation.iconName}
            kind={presentation.iconKind}
            title={presentation.title}
          />
        )
      }
      title={presentation.title}
      body={
        hasWarning || hasError
          ? getDetailedPopoverBody(statusMessages)
          : sourceTitle
      }
      position={position}
      canFlip={canFlip}
      isFocusable={showBadge}
    />
  );
}

export default QueueStatus;
