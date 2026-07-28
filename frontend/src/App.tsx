import { Navigate, Route, Routes } from 'react-router-dom'
import type { ReactElement } from 'react'
import { Layout } from './components/Layout'
import { Spinner } from './components/ui'
import { useSession } from './auth/SessionContext'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { HomePage } from './pages/HomePage'
import { LessonPage } from './pages/LessonPage'
import { QuizPage } from './pages/QuizPage'
import { ResultPage } from './pages/ResultPage'
import { ReviewPage } from './pages/ReviewPage'
import { RankingPage } from './pages/RankingPage'
import { HistoryPage } from './pages/HistoryPage'
import { ProfilePage } from './pages/ProfilePage'
import { AdminLayout } from './pages/admin/AdminLayout'
import { AdminHomePage } from './pages/admin/AdminHomePage'
import { AdminSeasonsPage } from './pages/admin/AdminSeasonsPage'
import { AdminRoundsPage } from './pages/admin/AdminRoundsPage'
import { AdminRoundEditorPage } from './pages/admin/AdminRoundEditorPage'
import { AdminRoundStatsPage } from './pages/admin/AdminRoundStatsPage'
import { AdminParticipantsPage } from './pages/admin/AdminParticipantsPage'

function Protected({ children, adminOnly = false }: { children: ReactElement; adminOnly?: boolean }) {
  const { me, loading } = useSession()

  if (loading) return <Spinner label="Verificando sua sessao..." />
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
              <AdminLayout />
            </Protected>
          }
        >
          <Route index element={<AdminHomePage />} />
          <Route path="temporadas" element={<AdminSeasonsPage />} />
          <Route path="rodadas" element={<AdminRoundsPage />} />
          <Route path="rodadas/:roundId" element={<AdminRoundEditorPage />} />
          <Route path="rodadas/:roundId/estatisticas" element={<AdminRoundStatsPage />} />
          <Route path="participantes" element={<AdminParticipantsPage />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
