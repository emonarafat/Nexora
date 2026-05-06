# Contributing to Nexora

Thank you for your interest in contributing to Nexora! We welcome contributions from everyone.

## Code of Conduct

Please be respectful, inclusive, and constructive in all interactions with the community.

## Getting Started

### Prerequisites

- **OS:** Windows, macOS, or Linux
- **.NET 10 SDK** or later
- **Docker & Docker Compose**
- **Git**

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/emonarafat/Nexora.git
   cd Nexora
   ```

2. **Start local services**
   ```bash
   docker-compose -f docker-compose.local.yml up -d
   ```

3. **Wait for services to be healthy**
   ```bash
   docker-compose -f docker-compose.local.yml logs -f
   # Wait for "Ready to accept connections" from each service
   ```

4. **Build the solution**
   ```bash
   dotnet build
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

6. **Start the Search API**
   ```bash
   dotnet run --project src/Nexora.SearchAPI/Nexora.SearchAPI.csproj
   ```

## Development Workflow

### Branch Naming

- **Features:** `feature/description` (e.g., `feature/add-personalization-engine`)
- **Bugfixes:** `fix/description` (e.g., `fix/typo-tolerance-crash`)
- **Documentation:** `docs/description` (e.g., `docs/api-contract`)
- **Chores:** `chore/description` (e.g., `chore/upgrade-dependencies`)

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(search): add fuzzy matching support
fix(ranking): correct BM25 score normalization
docs(api): update endpoint documentation
test(cache): add cache invalidation tests
chore(deps): upgrade Typesense client to v0.25
```

### Pull Request Process

1. Create a feature branch from `main`
2. Make your changes and commit with meaningful messages
3. Push to your fork and open a Pull Request
4. Ensure all CI checks pass (build, test, lint)
5. Request review from maintainers
6. Address feedback and re-request review
7. Squash commits if requested, then merge

### Code Style

**C# / .NET:**
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Run `dotnet format` before committing
- Use meaningful variable names (no single-letter variables except `i` in loops)
- Aim for ≤100 line methods

**File Organization:**
- One public class per file
- Namespace matches folder structure
- Order: usings → namespace → class → constructor → properties → public methods → private methods

### Testing

**Test Coverage Requirements:**
- ≥80% for new code
- ≥70% for modified code
- 100% for critical paths (ranking, caching, authentication)

**Test Structure:**
```csharp
public class SearchFeatureTests
{
    [Fact]
    public async Task SearchQuery_WithValidInput_ReturnsRelevantResults()
    {
        // Arrange
        var query = "running shoes";
        var handler = new SearchQueryHandler(...);

        // Act
        var result = await handler.Handle(new SearchQuery(query), CancellationToken.None);

        // Assert
        Assert.NotEmpty(result.Results);
        Assert.True(result.TotalCount > 0);
    }
}
```

**Run tests:**
```bash
dotnet test --collect:"XPlat Code Coverage" --configuration Release
```

## Areas to Contribute

### High Priority
- [ ] Performance optimization (latency reduction)
- [ ] Test coverage improvements
- [ ] Documentation updates
- [ ] Bug fixes

### Medium Priority
- [ ] Feature implementations (see [GitHub Issues](https://github.com/emonarafat/Nexora/issues))
- [ ] Dependency upgrades
- [ ] Code refactoring

### Low Priority
- [ ] Minor UI improvements (admin dashboard)
- [ ] Example scripts or tutorials

## Reporting Issues

Please use [GitHub Issues](https://github.com/emonarafat/Nexora/issues) to report bugs or request features.

**Include:**
- Clear description of the issue
- Steps to reproduce (for bugs)
- Expected vs. actual behavior
- Environment details (.NET version, OS, Docker version, etc.)
- Relevant logs or screenshots

## Documentation

If your change affects user-facing behavior:
1. Update relevant markdown files in `docs/`
2. Update API documentation comments in code (`///` for public members)
3. Add examples to `README.md` if applicable

## Performance Considerations

When contributing:
- **Avoid N+1 queries:** Batch database calls
- **Cache aggressively:** Use Valkey for hot data
- **Monitor latency:** Ensure changes don't exceed P95 budget (<100ms)
- **Profile before optimizing:** Use benchmarks (BenchmarkDotNet) for perf-critical code

## Security

- Never commit secrets (use `.env` files)
- Validate all user inputs
- Use parameterized queries to prevent SQL injection
- Report security vulnerabilities privately to maintainers (don't open public issues)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

**Questions?** Open a [Discussion](https://github.com/emonarafat/Nexora/discussions) or reach out to maintainers.

Happy coding! 🚀
