import React, { ReactNode } from 'react';
import { Stack } from 'Components/Layout';
import { Heading, Text } from 'Components/Typography';

interface PageIntroProps {
  title: ReactNode;
  description: ReactNode;
}

function PageIntro({ title, description }: PageIntroProps) {
  return (
    <Stack component="header" gap="small">
      <Heading level={1}>{title}</Heading>
      <Text tone="secondary" size="lead">
        {description}
      </Text>
    </Stack>
  );
}

export default PageIntro;
