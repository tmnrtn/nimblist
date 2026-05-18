import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

const ShareTargetPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    // The manifest share_target sends: url, text, title
    // Some apps put the URL in `text` rather than `url`, so check both
    const url = searchParams.get('url') ?? searchParams.get('text') ?? '';
    const trimmed = url.trim();
    if (trimmed.startsWith('http')) {
      navigate(`/recipes?import=${encodeURIComponent(trimmed)}`, { replace: true });
    } else {
      navigate('/recipes', { replace: true });
    }
  }, [navigate, searchParams]);

  return (
    <div className="flex justify-center items-center py-24">
      <p className="text-gray-500">Opening recipe import…</p>
    </div>
  );
};

export default ShareTargetPage;
