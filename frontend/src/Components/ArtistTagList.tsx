import React from 'react';
import { useSelector } from 'react-redux';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import TagList from './TagList';

interface ArtistTagListProps {
  tags: number[];
}

function ArtistTagList({ tags }: ArtistTagListProps) {
  const tagList = useSelector(createTagsSelector());

  return <TagList tags={tags} tagList={tagList} />;
}

export default ArtistTagList;
