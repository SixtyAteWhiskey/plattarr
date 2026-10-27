import React from 'react';
import Icon from 'Components/Icon';
import { Cluster, ResponsiveGrid, Stack } from 'Components/Layout';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { StatusBadge, StatusTone } from 'Components/Status';
import Surface from 'Components/Surface';
import { Heading, Text } from 'Components/Typography';
import { icons } from 'Helpers/Props';
import useHealthStatus from 'System/Status/Health/useHealthStatus';
import translate from 'Utilities/String/translate';
import styles from './System.css';

const systemItems = [
  {
    title: 'Status',
    summary: 'SystemStatusSummary',
    to: '/system/status',
    icon: icons.HEALTH,
  },
  {
    title: 'Tasks',
    summary: 'SystemTasksSummary',
    to: '/system/tasks',
    icon: icons.SCHEDULED,
  },
  {
    title: 'Backup',
    summary: 'SystemBackupSummary',
    to: '/system/backup',
    icon: icons.BACKUP,
  },
  {
    title: 'Updates',
    summary: 'SystemUpdatesSummary',
    to: '/system/updates',
    icon: icons.UPDATE,
  },
  {
    title: 'Events',
    summary: 'SystemEventsSummary',
    to: '/system/events',
    icon: icons.ACTIVITY,
  },
  {
    title: 'LogFiles',
    summary: 'SystemLogFilesSummary',
    to: '/system/logs/files',
    icon: icons.FILE,
  },
];

function SystemHealthStatus() {
  const { count, errors, warnings } = useHealthStatus();

  if (!count) {
    return null;
  }

  let icon = icons.INFO;
  let tone: StatusTone = 'info';

  if (errors) {
    icon = icons.DANGER;
    tone = 'danger';
  } else if (warnings) {
    icon = icons.WARNING;
    tone = 'warning';
  }

  const issueLabel = translate(count === 1 ? 'HealthIssue' : 'HealthIssues', {
    count,
  });

  return (
    <StatusBadge
      aria-label={issueLabel}
      icon={icon}
      label={issueLabel}
      tone={tone}
    />
  );
}

function System() {
  return (
    <PageContent title={translate('System')}>
      <PageContentBody>
        <Stack gap="xxLarge">
          <Stack component="header" gap="small" className={styles.pageHeader}>
            <Heading id="system-heading" level={1}>
              {translate('System')}
            </Heading>

            <Text tone="secondary" size="lead">
              {translate('SystemOverviewDescription')}
            </Text>
          </Stack>

          <nav aria-labelledby="system-heading">
            <Stack
              component="section"
              gap="medium"
              aria-labelledby="system-tools-heading"
            >
              <Heading id="system-tools-heading" level={2}>
                {translate('SystemTools')}
              </Heading>

              <ResponsiveGrid
                component="ul"
                minimum="wide"
                gap="medium"
                className={styles.grid}
              >
                {systemItems.map((item) => (
                  <li key={item.to} className={styles.item}>
                    <Link className={styles.cardLink} to={item.to}>
                      <Surface
                        className={styles.card}
                        elevation="low"
                        padding="medium"
                      >
                        <Stack gap="small">
                          <Cluster align="center" justify="between" gap="small">
                            <Cluster align="center" gap="small">
                              <Icon
                                className={styles.cardIcon}
                                name={item.icon}
                                size={18}
                                aria-hidden={true}
                              />

                              <Heading level={3} size="card">
                                {translate(item.title)}
                              </Heading>
                            </Cluster>

                            <Cluster
                              className={styles.cardMeta}
                              align="center"
                              gap="small"
                            >
                              {item.to === '/system/status' ? (
                                <SystemHealthStatus />
                              ) : null}

                              <Icon
                                className={styles.cardArrow}
                                name={icons.ARROW_RIGHT_NO_CIRCLE}
                                aria-hidden={true}
                              />
                            </Cluster>
                          </Cluster>

                          <Text tone="secondary">
                            {translate(item.summary)}
                          </Text>
                        </Stack>
                      </Surface>
                    </Link>
                  </li>
                ))}
              </ResponsiveGrid>
            </Stack>
          </nav>
        </Stack>
      </PageContentBody>
    </PageContent>
  );
}

export default System;
