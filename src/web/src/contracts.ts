export type QuestionType = 'text' | 'image' | 'audio'
export interface Question {
  id: string
  type: QuestionType
  prompt?: string | null
  media?: string | null
  answer: string
  alternatives: string[]
  notes?: string | null
  enabled: boolean
}
export interface Category {
  schemaVersion: 1
  id: string
  name: string
  description?: string | null
  coverImage?: string | null
  questions: Question[]
}
export interface CategorySet {
  schemaVersion: 1
  id: string
  name: string
  description?: string | null
  categoryIds: string[]
}
export interface Entry<T> { definition: T; revision: string }
export interface Library {
  categories: Entry<Category>[]
  sets: Entry<CategorySet>[]
  activeSetId: string | null
  errors: string[]
  dataDirectory: string
}
export type Player = 'a' | 'b'
export type DuelStatus = 'ready' | 'running' | 'paused' | 'finished'
export type DuelAction = 'start' | 'pause' | 'resume' | 'correct' | 'pass' | 'undo' | 'append' | 'end' | 'restart' | 'return'
export interface Configuration {
  playerA: string
  playerB: string
  categoryId: string
  seconds: number
  passSeconds: number
  starter: Player | 'random'
  order: 'ordered' | 'shuffled'
}
export interface PublicQuestion { id: string; type: QuestionType; prompt: string | null; mediaUrl: string | null }
export interface AudienceDuel {
  id: string
  version: number
  status: DuelStatus
  categoryName: string
  playerA: string
  playerB: string
  aMilliseconds: number
  bMilliseconds: number
  active: Player
  winner: Player | null
  question: PublicQuestion | null
  sampledAt: number
}
export interface HostDuel {
  duel: AudienceDuel
  categorySetId: string
  configuration: Configuration
  answer: string | null
  alternatives: string[]
  notes: string | null
  canUndo: boolean
  exhausted: boolean
}
export interface HostState { session: HostDuel | null; audienceCount: number }
