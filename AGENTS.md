# Project Guidelines

## Commit Conventions

All commits MUST follow the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, no logic change)
- **refactor**: Code refactoring (no feature or bug fix)
- **perf**: Performance improvements
- **test**: Adding or updating tests
- **build**: Build system or dependency changes
- **ci**: CI/CD configuration changes
- **chore**: Maintenance tasks

### Examples

```
feat(browser-tabs): add edge browser support
fix(activation): resolve tab focus issue on multi-monitor setup
docs: update installation instructions
refactor(discovery): simplify tab enumeration logic
```

### Rules

- Use lowercase for type and description
- Keep description under 72 characters
- Use imperative mood ("add" not "added" or "adds")
- Breaking changes MUST include `BREAKING CHANGE:` in footer or `!` after type
