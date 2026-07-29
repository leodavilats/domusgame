export type RoundAvailability = 'Draft' | 'Scheduled' | 'Open' | 'Closed'
export type RoundStatus = 'Draft' | 'Published'
export type AttemptStatus = 'InProgress' | 'Completed'
export type AnswerOutcome = 'Pending' | 'Correct' | 'Incorrect' | 'Blank' | 'TimedOut'
export type QuestionMediaType = 'None' | 'Image' | 'Audio'
export type ParticipantRole = 'Participant' | 'Admin'
export type SeasonStatus = 'Draft' | 'Active' | 'Finished'

export interface MyRoomSummary {
  id: string
  name: string
}

export interface MyRoom {
  id: string
  name: string
  joinedAt: string
  memberCount: number
}

export interface Me {
  id: string
  displayName: string
  avatarUrl?: string | null
  isAdmin: boolean
  room?: MyRoomSummary | null
}

export interface RoundSummary {
  id: string
  seasonId: string
  weekNumber: number
  title: string
  availability: RoundAvailability
  opensAt: string
  closesAt: string
  questionCount: number
  maxPoints: number
  pointsPerCorrectAnswer: number
  maxSpeedBonus: number
  questionTimeLimitSeconds: number
}

export interface MyAttemptSummary {
  attemptId: string
  status: AttemptStatus
  answeredCount: number
  questionCount: number
  totalPoints?: number | null
  correctCount?: number | null
  totalTimeMs?: number | null
  position?: number | null
}

export interface Lesson {
  title: string
  scriptureReference: string
  content: string
  externalUrl?: string | null
}

export interface AttemptOption {
  id: string
  text: string
}

export interface AttemptQuestion {
  id: string
  order: number
  totalQuestions: number
  text: string
  mediaType: QuestionMediaType
  mediaUrl?: string | null
  options: AttemptOption[]
  timeLimitSeconds: number
  servedAt: string
  deadlineAt: string
  serverNow: string
}

export interface AttemptState {
  attemptId: string
  roundId: string
  status: AttemptStatus
  questionCount: number
  answeredCount: number
  currentQuestion?: AttemptQuestion | null
}

export interface SubmitAnswerResponse {
  answerId: string
  timedOut: boolean
  attemptFinished: boolean
  nextQuestion?: AttemptQuestion | null
}

export interface AttemptResult {
  attemptId: string
  round: RoundSummary
  status: AttemptStatus
  totalPoints: number
  maxPoints: number
  correctCount: number
  questionCount: number
  totalTimeMs: number
  answersRevealed: boolean
  position?: number | null
}

export interface ReviewOption {
  id: string
  text: string
  isCorrect: boolean
}

export interface ReviewQuestion {
  questionId: string
  order: number
  text: string
  mediaType: QuestionMediaType
  mediaUrl?: string | null
  explanation?: string | null
  options: ReviewOption[]
  selectedOptionId?: string | null
  outcome: AnswerOutcome
  points: number
  elapsedMs: number
}

export interface RoundReview {
  round: RoundSummary
  lesson: Lesson
  totalPoints: number
  maxPoints: number
  correctCount: number
  totalTimeMs: number
  questions: ReviewQuestion[]
}

export interface RankingEntry {
  position: number
  participantId: string
  displayName: string
  avatarUrl?: string | null
  totalPoints: number
  totalTimeMs: number
  roundsPlayed: number
  isMe: boolean
}

export interface Ranking {
  scope: string
  title: string
  entries: RankingEntry[]
  me?: RankingEntry | null
}

export interface SeasonInfo {
  id: string
  name: string
  startsOn: string
  endsOn: string
}

export interface DashboardActions {
  canStart: boolean
  canResume: boolean
  canSeeResult: boolean
  canReview: boolean
}

export interface MyStats {
  seasonPoints: number
  position?: number | null
  participantsCount: number
  streak: number
  roundsPlayed: number
}

export interface Dashboard {
  room?: MyRoomSummary | null
  season?: SeasonInfo | null
  round?: RoundSummary | null
  lessonTitle?: string | null
  lessonReference?: string | null
  myAttempt?: MyAttemptSummary | null
  actions: DashboardActions
  stats: MyStats
  nextRoundOpensAt?: string | null
  serverNow: string
}

export interface RoundListItem {
  round: RoundSummary
  myAttempt?: MyAttemptSummary | null
}

export interface RoundDetail {
  round: RoundSummary
  lesson?: Lesson | null
  myAttempt?: MyAttemptSummary | null
  serverNow: string
}

export interface PodiumEntry {
  position: number
  displayName: string
  totalPoints: number
  totalTimeMs: number
}

export interface AdminSeason {
  id: string
  name: string
  startsOn: string
  endsOn: string
  status: SeasonStatus
  roundCount: number
  publishedRoundCount: number
  podium: PodiumEntry[]
}

export interface AdminOption {
  id: string
  order: number
  text: string
  isCorrect: boolean
}

export interface AdminQuestion {
  id: string
  order: number
  text: string
  mediaType: QuestionMediaType
  mediaUrl?: string | null
  explanation?: string | null
  options: AdminOption[]
}

export interface AdminRoundListItem {
  round: RoundSummary
  status: RoundStatus
  attemptCount: number
  canEdit: boolean
  canDelete: boolean
}

export interface AdminRound {
  round: RoundSummary
  status: RoundStatus
  lesson: Lesson
  questions: AdminQuestion[]
  problems: string[]
  attemptCount: number

  canEdit: boolean
  canDelete: boolean
}

export interface AdminParticipant {
  id: string
  displayName: string
  avatarUrl?: string | null
  role: ParticipantRole
  isRemoved: boolean
  joinedAt: string
  seasonPoints: number
  roundsPlayed: number
  lastAttemptAt?: string | null
}

export interface Invite {
  roomName: string
  inviteCode: string
  rotatedAt: string
  memberCount: number
}

export interface QuestionStat {
  questionId: string
  order: number
  text: string
  answers: number
  correct: number
  accuracy: number
  averageSeconds: number
}

export interface RoundStats {
  round: RoundSummary
  participantCount: number
  attemptCount: number
  finishedCount: number
  participationRate: number
  averagePoints: number
  medianPoints: number
  averageSecondsPerQuestion: number
  questions: QuestionStat[]
  missing: string[]
}

export interface WeekParticipation {
  roundId: string
  weekNumber: number
  title: string
  availability: RoundAvailability
  attempts: number
  averagePoints: number
}

export interface Overview {
  seasonId?: string | null
  seasonName?: string | null
  participantCount: number
  adminCount: number
  weeks: WeekParticipation[]
}

export interface ToolsDiagnostics {
  enabled: boolean
  environment: string
  serverNowUtc: string
  timeZoneHint: string
  activeSeasonName?: string | null
  appliedMigration?: string | null
  seasons: number
  rounds: number
  questions: number
  participants: number
  attempts: number
  answers: number
}

export interface AuditEntry {
  occurredAt: string
  actorName: string
  action: string
  details?: string | null
}

export interface ToolActionResult {
  message: string
}
