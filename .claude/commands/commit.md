Review all staged changes with `git diff --cached`.

If nothing is staged:

1. Run `git status` to identify modified, untracked, or deleted files
2. Evaluate which files are relevant to a logical, atomic commit
3. Stage only those files — never stage unrelated changes together

Analyze the diff carefully before writing the commit message.

Follow Conventional Commits (https://www.conventionalcommits.org):

Format:
<type>(<optional scope>): <short summary>

[optional body]

[optional footers]

Types: feat, fix, refactor, chore, revert, test, docs, ci, infra

Rules:

- Subject line: imperative mood, ≤72 chars, no trailing period
- Body: explain _what_ changed and _why_ (not how), wrapped at 72 chars
- Use BREAKING CHANGE: footer if applicable
- Never add Co-Authored-By or AI attribution trailers

Examples of good subjects:

- feat(auth): add OAuth2 login with Google provider
- fix(api): handle null response when user session expires
- refactor: extract validation logic into separate module

Create the commit. Then run `git log -1 --oneline` to confirm it succeeded.

Include at the footer of the commit message, if passed as argument, the link of the github issue.
Example:
For: `/commit #xx`
At the footer:

- Related issue: github issue link