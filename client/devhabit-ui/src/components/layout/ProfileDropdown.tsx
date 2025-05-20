import { Link } from 'react-router-dom';
import { HiUser, HiCog8Tooth, HiArrowRightOnRectangle } from 'react-icons/hi2';
import { useAuth0 } from '@auth0/auth0-react';

const ProfileDropdown = () => {
  const { logout } = useAuth0();

  const handleLogout = () => {
    logout({ logoutParams: { returnTo: window.location.origin } });
  };

  return (
    <div className="absolute right-0 top-full mt-2 w-48 bg-white rounded-lg shadow-lg border border-gray-200 py-1">
      <Link
        to="/profile"
        className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100"
      >
        <HiUser className="w-4 h-4" />
        Profile
      </Link>

      <Link
        to="/settings"
        className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100"
      >
        <HiCog8Tooth className="w-4 h-4" />
        Settings
      </Link>

      <hr className="my-1 border-gray-200" />

      <button
        onClick={handleLogout}
        className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 w-full"
      >
        <HiArrowRightOnRectangle className="w-4 h-4" />
        Logout
      </button>
    </div>
  );
};

export default ProfileDropdown;
