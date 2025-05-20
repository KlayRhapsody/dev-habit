import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import MainLayout from './components/layout/MainLayout';
import ProtectedRoute from './components/auth/ProtectedRoute';
import Login from './features/auth/Login';
import Dashboard from './pages/Dashboard';
import Profile from './features/users/Profile';
import { CreateHabitPage } from './features/habits/CreateHabitPage';
import { HabitDetailsPage } from './features/habits/HabitDetailsPage';
import { EditHabitPage } from './features/habits/EditHabitPage';
import { HabitsPage } from './features/habits/HabitsPage';
import { TagsPage } from './features/tags/TagsPage';
import { EntriesPage } from './features/entries/EntriesPage';
import { CreateEntryPage } from './features/entries/CreateEntryPage';
import { EditEntryPage } from './features/entries/EditEntryPage';
import { CreateBatchEntriesPage } from './features/entries/CreateBatchEntriesPage';
import { EntryImportsPage } from './features/entries/EntryImportsPage';
import { Auth0Provider } from '@auth0/auth0-react';

export default function App() {
  return (
    <Auth0Provider
      domain={import.meta.env.VITE_AUTH0_DOMAIN}
      clientId={import.meta.env.VITE_AUTH0_CLIENT_ID}
      authorizationParams={{
        redirect_uri: window.location.origin,
        audience: import.meta.env.VITE_AUTH0_AUDIENCE,
        scope: 'openid profile email',
      }}
    >
      <BrowserRouter>
          <Routes>
            {/* Auth routes */}
            <Route path="/login" element={<Login />} />

          {/* Protected routes */}
          <Route
            element={
              <ProtectedRoute>
                <MainLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/profile" element={<Profile />} />
            <Route path="/habits" element={<HabitsPage />} />
            <Route path="/habits/create" element={<CreateHabitPage />} />
            <Route path="/habits/:id" element={<HabitDetailsPage />} />
            <Route path="/habits/:id/edit" element={<EditHabitPage />} />
            <Route path="/entries" element={<EntriesPage />} />
            <Route path="/entries/create" element={<CreateEntryPage />} />
            <Route path="/entries/create-batch" element={<CreateBatchEntriesPage />} />
            <Route path="/entries/:id/edit" element={<EditEntryPage />} />
            <Route path="/tags" element={<TagsPage />} />
            <Route path="/entries/imports" element={<EntryImportsPage />} />
          </Route>

          {/* Catch all */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </Auth0Provider>
  );
}
