import { query } from "$app/server";
import { z } from "zod";

const emptySchema = z.object({});

// GitHub API configuration
const GITHUB_OWNER = "nightscout";
const GITHUB_REPO = "nocturne";
const GITHUB_API_BASE = "https://api.github.com";

// Cache for GitHub data (5 minute TTL)
interface CacheEntry<T> {
  data: T;
  timestamp: number;
}
const CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes
const githubCache = new Map<string, CacheEntry<unknown>>();

function getCached<T>(key: string): T | null {
  const entry = githubCache.get(key);
  if (!entry) return null;
  if (Date.now() - entry.timestamp > CACHE_TTL_MS) {
    githubCache.delete(key);
    return null;
  }
  return entry.data as T;
}

function setCache<T>(key: string, data: T): void {
  githubCache.set(key, { data, timestamp: Date.now() });
}

// GitHub API schemas
const githubUserSchema = z.object({
  login: z.string(),
  avatar_url: z.string(),
  html_url: z.string(),
});

const githubMilestoneSchema = z.object({
  id: z.number(),
  number: z.number(),
  title: z.string(),
  description: z.string().nullable(),
  state: z.enum(["open", "closed"]),
  open_issues: z.number(),
  closed_issues: z.number(),
  created_at: z.string(),
  updated_at: z.string(),
  due_on: z.string().nullable(),
  closed_at: z.string().nullable(),
  html_url: z.string(),
});

const githubLabelSchema = z.object({
  id: z.number(),
  name: z.string(),
  color: z.string(),
  description: z.string().nullable().optional(),
});

const githubIssueSchema = z.object({
  id: z.number(),
  number: z.number(),
  title: z.string(),
  state: z.enum(["open", "closed"]),
  html_url: z.string(),
  created_at: z.string(),
  updated_at: z.string(),
  closed_at: z.string().nullable(),
  user: githubUserSchema.nullable(),
  labels: z.array(githubLabelSchema),
  assignees: z.array(githubUserSchema),
  pull_request: z.object({}).optional(),
});

export type GitHubMilestone = z.infer<typeof githubMilestoneSchema>;
export type GitHubIssue = z.infer<typeof githubIssueSchema>;
export type GitHubLabel = z.infer<typeof githubLabelSchema>;

export interface RoadmapMilestone extends GitHubMilestone {
  issues: GitHubIssue[];
  progress: number;
}

// Fetch milestones from GitHub
export const getRoadmapData = query(emptySchema, async () => {
  const cacheKey = "roadmap-data";
  const cached = getCached<RoadmapMilestone[]>(cacheKey);
  if (cached) {
    return cached;
  }

  const headers: HeadersInit = {
    Accept: "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
    "User-Agent": "Nocturne-Portal",
  };

  // Fetch all milestones (open and closed)
  const milestonesResponse = await fetch(
    `${GITHUB_API_BASE}/repos/${GITHUB_OWNER}/${GITHUB_REPO}/milestones?state=all&sort=due_on&direction=asc&per_page=100`,
    { headers, signal: AbortSignal.timeout(10000) }
  );

  if (!milestonesResponse.ok) {
    throw new Error(`Failed to fetch milestones: ${milestonesResponse.status}`);
  }

  const milestonesData: unknown = await milestonesResponse.json();
  const milestones = z.array(githubMilestoneSchema).parse(milestonesData);

  // Fetch issues for each milestone
  const roadmapMilestones: RoadmapMilestone[] = await Promise.all(
    milestones.map(async (milestone) => {
      const issuesResponse = await fetch(
        `${GITHUB_API_BASE}/repos/${GITHUB_OWNER}/${GITHUB_REPO}/issues?milestone=${milestone.number}&state=all&per_page=100`,
        { headers, signal: AbortSignal.timeout(10000) }
      );

      let issues: GitHubIssue[] = [];
      if (issuesResponse.ok) {
        const issuesData: unknown = await issuesResponse.json();
        const allIssues = z.array(githubIssueSchema).parse(issuesData);
        // Filter out pull requests (issues endpoint includes PRs)
        issues = allIssues.filter((issue) => !issue.pull_request);
      }

      const totalIssues = milestone.open_issues + milestone.closed_issues;
      const progress = totalIssues > 0 ? (milestone.closed_issues / totalIssues) * 100 : 0;

      return {
        ...milestone,
        issues,
        progress,
      };
    })
  );

  setCache(cacheKey, roadmapMilestones);
  return roadmapMilestones;
});
