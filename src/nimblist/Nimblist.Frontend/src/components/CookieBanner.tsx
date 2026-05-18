import { Link } from 'react-router-dom';

interface Props {
  onAccept: () => void;
  onDecline: () => void;
}

export default function CookieBanner({ onAccept, onDecline }: Props) {
  return (
    <div className="fixed bottom-0 left-0 right-0 z-50 p-4 bg-gray-900 text-white shadow-xl">
      <div className="max-w-4xl mx-auto flex flex-col sm:flex-row items-start sm:items-center gap-4">
        <p className="text-sm text-gray-300 flex-1">
          We use analytics cookies to understand how Nimblist is used and improve the experience.
          No personal data is sold or shared.{' '}
          <Link to="/privacy" className="underline hover:text-white">Privacy Policy</Link>.
        </p>
        <div className="flex gap-2 flex-shrink-0">
          <button
            onClick={onDecline}
            className="px-4 py-2 text-sm font-medium text-gray-300 border border-gray-600 rounded-md hover:bg-gray-700 transition-colors"
          >
            Decline
          </button>
          <button
            onClick={onAccept}
            className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-500 transition-colors"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  );
}
