#!/usr/bin/env python3
"""PreToolUse guard: force the yes/no permission prompt where a plain
permission rule cannot see the action.

Permission rules match the command text Claude usually writes, so they miss
other forms of the same action (`gh api` instead of `gh pr merge`,
`git -c … push`, an environment-variable prefix, `curl` to the API), and they
cannot look inside an MCP call's arguments. This hook reads the whole call and
answers "ask" for those cases; everything else falls through to the rules.
The reference copy lives in ~/.claude/hooks/; a repository may mirror it.
"""
import json
import re
import sys

VALUE = r"""(?:'[^']*'|"[^"]*"|[^\s'"])+"""
GIT_OPTS = rf"(?:\s+(?:-c\s*{VALUE}|-C\s*{VALUE}|--[\w.-]+(?:={VALUE})?|-[a-zA-Z]))*"

BASH_RULES = [
    (rf"\bgit{GIT_OPTS}\s+push\b", "git push"),
    (rf"\bgit{GIT_OPTS}\s+(?:rebase|reset|clean|rm)\b", "rewrites history or removes files"),
    (rf"\bgit{GIT_OPTS}\s+stash(?!\s+(?:list|show)\b)\b", "git stash"),
    (rf"\bgit{GIT_OPTS}\s+commit\b[^;&|\n]*--amend\b", "git commit --amend"),
    (rf"\bgit{GIT_OPTS}\s+branch\b[^;&|\n]*\s(?:-d|-D|--delete)\b", "deletes a branch"),
    (rf"\bgit{GIT_OPTS}\s+worktree\s+(?:remove|prune)\b", "removes a worktree"),
    (r"\bgh\s+pr\s+(?:create|edit|comment|merge|close|reopen|ready|review)\b", "changes a pull request"),
    (r"\bgh\s+issue\s+(?:create|edit|close|reopen|comment|delete|transfer|pin|unpin|lock|unlock)\b", "changes a GitHub issue"),
    (r"\bgh\s+(?:secret|variable)\s+(?:set|delete|remove)\b", "changes repository secrets or variables"),
    (r"\bgh\s+label\s+(?:create|edit|delete|clone)\b", "changes repository labels"),
    (r"\bgh\s+repo\s+(?:edit|delete|rename|archive|unarchive|sync)\b", "changes repository settings"),
]

WRITE_HOSTS = r"(?:api\.github\.com|uploads\.github\.com|api\.linear\.app|api\.render\.com|console\.neon\.tech|sentry\.io)"
WRITE_SQL = re.compile(
    r"\b(?:insert|update|delete|drop|alter|create|truncate|grant|revoke|copy|call|merge|"
    r"vacuum|reindex|cluster|refresh|lock|comment\s+on|security\s+label|import)\b",
    re.I,
)


def segments(command):
    """Split a shell command on its separators; good enough for matching."""
    return [s for s in re.split(r"&&|\|\||[;|\n]", command) if s.strip()]


def gh_api_writes(seg):
    if not re.search(r"\bgh\s+api\b", seg):
        return False
    method = re.search(r"(?:-X|--method)[\s=]*['\"]?([A-Za-z]+)", seg)
    if method:
        return method.group(1).upper() != "GET"
    has_fields = re.search(r"\s(?:-f|-F|--field|--raw-field|--input)\b", seg)
    if not has_fields:
        return False
    if re.search(r"\bgraphql\b", seg):
        return bool(re.search(r"\bmutation\b", seg))
    return True


def http_writes(seg):
    if not re.search(r"\b(?:curl|wget|http|https)\b", seg) or not re.search(WRITE_HOSTS, seg):
        return False
    return bool(re.search(r"(?:-X|--request|--method)[\s=]*['\"]?(?:POST|PUT|PATCH|DELETE)|\s(?:-d|--data[\w-]*|-F|--form|--json)\b", seg, re.I))


def rm_outside_tmp(seg):
    m = re.search(r"(?:^|\s)(?:sudo\s+)?(?:rm|rmdir|unlink)\s+(.*)$", seg.strip())
    if not m:
        return False
    # The container's python3 is the minimal build, which has no shlex.
    args = [a.strip("'\"") for a in re.findall(r"'[^']*'|\"[^\"]*\"|\S+", m.group(1))]
    targets = [a for a in args if not a.startswith("-")]
    return not targets or any(not re.match(r"^/tmp/", t) for t in targets)


def check_bash(command):
    for pattern, reason in BASH_RULES:
        if re.search(pattern, command):
            return reason
    for seg in segments(command):
        if gh_api_writes(seg):
            return "gh api call that writes"
        if http_writes(seg):
            return "HTTP request that writes to an external API"
        if rm_outside_tmp(seg):
            return "deletes files outside /tmp"
    return None


def check(call):
    name = call.get("tool_name", "")
    args = call.get("tool_input") or {}
    if name == "Bash":
        return check_bash(args.get("command", ""))
    if name == "mcp__claude_ai_Linear__save_issue" and not args.get("id"):
        return "creates a new Linear ticket"
    if name == "mcp__claude_ai_Neon__run_sql" and WRITE_SQL.search(args.get("sql", "")):
        return "SQL that changes the database"
    return None


def main():
    reason = check(json.load(sys.stdin))
    if reason:
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "ask",
            "permissionDecisionReason": f"Guard: {reason}. Needs the maintainer's yes.",
        }}))


if __name__ == "__main__":
    main()
