# 📚 Coding Conventions Documentation - BIZCORE ERP

> **Welcome!** This folder contains comprehensive guidelines for writing consistent, maintainable code across the Bizcore ERP project.

---

## 📖 Documentation Structure

### 1. **CODING_CONVENTIONS.md** (Main Document)
   - **Purpose**: Complete reference guide for all coding standards
   - **Audience**: All developers
   - **Size**: ~140 sections covering all aspects
   - **Contents**:
     - Naming conventions (PascalCase, camelCase, interfaces, etc.)
     - Project organization (4-layer DDD architecture)
     - Clean code principles (SOLID, DRY)
     - Exception handling strategies
     - Logging and observability
     - Async/Event-driven patterns
     - Security and authorization
     - Database and EF Core conventions
     - Testing conventions
     - Frontend (React/TypeScript) standards

   **When to use**: Reference for detailed explanations and examples

### 2. **CONVENTIONS_QUICK_REFERENCE.md** (Quick Lookup)
   - **Purpose**: Fast reference for busy developers
   - **Audience**: Experienced team members, code reviewers
   - **Size**: 1-2 minute read
   - **Contents**:
     - Most important 5 rules (enforce strictly)
     - Naming conventions at a glance
     - Layer responsibilities diagram
     - Code templates (ready to copy-paste)
     - Logging template
     - Common patterns

   **When to use**: Quick lookup during development or code review

### 3. **CODE_REVIEW_GUIDE.md** (Review Standards)
   - **Purpose**: Standards for conducting effective code reviews
   - **Audience**: Code reviewers, senior developers
   - **Size**: ~150 checklist items
   - **Contents**:
     - 10-section review checklist (architecture, naming, async, etc.)
     - Step-by-step review process
     - Approval criteria
     - When to request changes
     - How to write good review comments
     - Reviewer training guide

   **When to use**: Before reviewing PRs, or when defining review standards

### 4. **.editorconfig** (Automated Enforcement)
   - **Purpose**: IDE/editor configuration for automatic style enforcement
   - **Tools**: Supports VS Code, Visual Studio, Rider, etc.
   - **Contents**:
     - Indentation and formatting rules
     - Naming convention rules (automatic checks)
     - Code style rules (C#, JSON, YAML, Markdown)
     - Spacing and brace preferences

   **When to use**: Automatically enforced by editors - no manual action needed

---

## 🎯 Quick Navigation

### By Role

**👨‍💻 New Developer**
1. Read: Section 1-3 of CODING_CONVENTIONS.md (overview, naming, structure)
2. Review: CONVENTIONS_QUICK_REFERENCE.md (essentials)
3. Reference: CODING_CONVENTIONS.md sections as needed

**🔍 Code Reviewer**
1. Use: CODE_REVIEW_GUIDE.md (checklist & process)
2. Reference: CONVENTIONS_QUICK_REFERENCE.md (quick checks)
3. Deep dive: CODING_CONVENTIONS.md (specific questions)

**🏗️ Architect/Lead**
1. Review: CODING_CONVENTIONS.md (entire document)
2. Discuss: CODE_REVIEW_GUIDE.md (review standards)
3. Maintain: Update conventions as needed

**✅ Tech Lead / Onboarding**
1. Share: CONVENTIONS_QUICK_REFERENCE.md (5-min intro)
2. Discuss: Top 5 rules from CONVENTIONS_QUICK_REFERENCE.md
3. Point to: CODING_CONVENTIONS.md for deeper learning

### By Topic

| Topic | Document | Section |
|-------|----------|---------|
| **Naming** | CODING_CONVENTIONS.md | Section 2 |
| **Architecture** | CODING_CONVENTIONS.md | Sections 3-4 |
| **Exceptions** | CODING_CONVENTIONS.md | Section 5 |
| **Logging** | CODING_CONVENTIONS.md | Section 6 |
| **Async** | CODING_CONVENTIONS.md | Section 7 |
| **Security** | CODING_CONVENTIONS.md | Section 8 |
| **Database** | CODING_CONVENTIONS.md | Section 9 |
| **Testing** | CODING_CONVENTIONS.md | Section 10 |
| **Code Review** | CODE_REVIEW_GUIDE.md | All sections |
| **Templates** | CONVENTIONS_QUICK_REFERENCE.md | Code Templates |
| **IDE Setup** | .editorconfig | N/A |

---

## ⚡ Most Important Rules (Enforce Strictly!)

These 5 rules are **non-negotiable** and should be checked in every PR:

### 1. **No Business Logic in Controllers**
Business logic belongs in services (Application layer), not controllers (API layer).

### 2. **Always Add Authorization**
All public endpoints must explicitly require an authorization policy.

### 3. **Use Events for Inter-Service Communication**
Services communicate via events (MassTransit), not direct HTTP calls.

### 4. **Throw Domain Exceptions**
Use typed exceptions (DomainException, NotFoundException) instead of return codes.

### 5. **Use Async/Await Everywhere**
All I/O operations must be async. Method names must end with `Async`.

See **CONVENTIONS_QUICK_REFERENCE.md** section "Most Important Rules" for code examples.

---

## 🛠 Setting Up Your Environment

### Visual Studio / Rider
- `.editorconfig` automatically loaded
- Install StyleCop Analyzers (recommended)
- Enable "Format document on save" in settings

### VS Code
- Install "EditorConfig for VS Code" extension
- Install "C# Dev Kit" extension
- StyleCop rules apply via analyzer

### Validate Your Setup
1. Open a C# file
2. Create a private field: `private var myVariable;`
3. Save file - should auto-correct to `private var _myVariable;`
4. If not, check extension installed and enabled

---

## 📋 Checklist for Team Leaders

When rolling out these conventions:

- [ ] Share CONVENTIONS_QUICK_REFERENCE.md with team (5 min read)
- [ ] Discuss "5 Most Important Rules" in team meeting
- [ ] Add CODE_REVIEW_GUIDE.md to code review process
- [ ] Configure .editorconfig in IDE
- [ ] Set up StyleCop analyzers in build
- [ ] Add convention checks to CI/CD (SonarQube, StyleCop)
- [ ] Link this README in project documentation
- [ ] Conduct onboarding session for new team members
- [ ] Review first few PRs with focus on conventions
- [ ] Adjust conventions based on team feedback

---

## 🔄 Updating These Guidelines

### When to Update
- New architectural pattern adopted
- Framework upgraded (ASP.NET, EF Core, etc.)
- Team agrees on convention change
- Common mistakes identified

### How to Update
1. Edit relevant document (CODING_CONVENTIONS.md, etc.)
2. Add timestamp and version
3. Notify team of changes
4. Discuss in next team sync
5. Update CODE_REVIEW_GUIDE.md if needed

### Version History
- **v1.0** (2024-05-07): Initial comprehensive guide

---

## 📞 Questions or Issues?

If conventions are unclear or seem inconsistent:

1. Check related sections in **CODING_CONVENTIONS.md**
2. Review examples in **CONVENTIONS_QUICK_REFERENCE.md**
3. Ask in code review or team sync
4. Suggest update to lead/architect

---

## 🎓 Related Documents

- **[PROJECT_INDEX.md](../02-project-overview/PROJECT_INDEX.md)** - Project overview and architecture
- **[PROJECT_STRUCTURE.md](../02-project-overview/PROJECT_STRUCTURE.md)** - Detailed project organization
- **[TRANSACTION_MANAGEMENT_DESIGN.md](../05-transactions/TRANSACTION_MANAGEMENT_DESIGN.md)** - Transaction patterns
- **[IDEMPOTENCY_DESIGN.md](../03-architecture/IDEMPOTENCY_DESIGN.md)** - Idempotency patterns

---

## 📊 Document Statistics

| Document | Lines | Topics | Code Examples |
|----------|-------|--------|----------------|
| CODING_CONVENTIONS.md | ~1,200 | 12 major | 80+ |
| CONVENTIONS_QUICK_REFERENCE.md | ~350 | 8 | 30+ |
| CODE_REVIEW_GUIDE.md | ~450 | 10 | 25+ |
| .editorconfig | ~250 | N/A | N/A |

**Total**: Comprehensive coverage with practical examples for every major topic.

---

## ✅ Quality Gates

These conventions ensure:
- ✅ **Consistency**: All code follows same style
- ✅ **Maintainability**: Code is easy to understand and modify
- ✅ **Reliability**: Architecture patterns prevent common bugs
- ✅ **Security**: Authorization and data protection enforced
- ✅ **Performance**: Async patterns and query optimization
- ✅ **Testability**: Clean code structure enables testing

---

**Last Updated**: 2024-05-07  
**Maintained By**: Architecture Team  
**Status**: Active - All team members must follow

