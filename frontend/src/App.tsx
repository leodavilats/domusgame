import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import type { ReactElement } from 'react'
import { Layout } from './components/Layout'
import { Spinner } from './components/ui'
import { useSession } from './auth/SessionContext'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { HomePage } from './pages/HomePage'
import { JoinRoomPage } from './pages/JoinRoomPage'
import { LessonPage } from './pages/LessonPage'
import { QuizPage } from './pages/QuizPage'
import { ResultPage } from './pages/ResultPage'
import { ReviewPage } from './pages/ReviewPage'
import { RankingPage } from './pages/RankingPage'
import { HistoryPage } from './pages/HistoryPage'
import { ProfilePage } from './pages/ProfilePage'

const AdminLayout = lazy(() => import('./pages/admin/AdminLayout').then((m) => ({ default: m.AdminLayout })))
const AdminHomePage = lazy(() => import('./pages/admin/AdminHomePage').then((m) => ({ default: m.AdminHomePage })))
const AdminSeasonsPage = lazy(() => import('./pages/admin/AdminSeasonsPage').then((m) => ({ default: m.AdminSeasonsPage })))
const AdminRoundsPage = lazy(() => import('./pages/admin/AdminRoundsPage').then((m) => ({ default: m.AdminRoundsPage })))
const AdminRoundEditorPage = lazy(() => import('./pages/admin/AdminRoundEditorPage').then((m) => ({ default: m.AdminRoundEditorPage })))
const AdminRoundStatsPage = lazy(() => import('./pages/admin/AdminRoundStatsPage').then((m) => ({ default: m.AdminRoundStatsPage })))
const AdminParticipantsPage = lazy(() => import('./pages/admin/AdminParticipantsPage').then((m) => ({ default: m.AdminParticipantsPage })))
const AdminToolsPage = lazy(() => import('./pages/admin/AdminToolsPage').then((m) => ({ default: m.AdminToolsPage })))

function Protected({ children, adminOnly = false }: { children: ReactElement; adminOnly?: boolean }) {
  const { me, loading } = useSession()

  if (loading) return <Spinner label="Verificando sua sessão..." />
  if (!me) return <Navigate to="/entrar" replace />
  if (adminOnly && !me.isAdmin) return <Navigate to="/" replace />

  return children
}

export function App() {
  return (
    <Routes>
      <Route path="/entrar" element={<LoginPage />} />
      <Route path="/cadastro" element={<RegisterPage />} />

      <Route
        element={
          <Protected>
            <Layout />
          </Protected>
        }
      >
        <Route path="/" element={<HomePage />} />
        <Route path="/sala" element={<JoinRoomPage />} />
        <Route path="/rodadas/:roundId/licao" element={<LessonPage />} />
        <Route path="/rodadas/:roundId/quiz" element={<QuizPage />} />
        <Route path="/rodadas/:roundId/revisao" element={<ReviewPage />} />
        <Route path="/tentativas/:attemptId/resultado" element={<ResultPage />} />
        <Route path="/ranking" element={<RankingPage />} />
        <Route path="/historico" element={<HistoryPage />} />
        <Route path="/perfil" element={<ProfilePage />} />

        <Route
          path="/admin"
          element={
            <Protected adminOnly>
              <Suspense fallback={<Spinner label="Carregando administração..." />}>
                <AdminLayout />
              </Suspense>
            </Protected>
          }
        >
          <Route index element={<AdminHomePage />} />
          <Route path="temporadas" element={<AdminSeasonsPage />} />
          <Route path="rodadas" element={<AdminRoundsPage />} />
          <Route path="rodadas/:roundId" element={<AdminRoundEditorPage />} />
          <Route path="rodadas/:roundId/estatisticas" element={<AdminRoundStatsPage />} />
          <Route path="participantes" element={<AdminParticipantsPage />} />
          <Route path="ferramentas" element={<AdminToolsPage />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
