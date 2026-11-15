# Backend, Frontend and Security Concepts

A comprehensive reference list of software engineering concepts for reviewing, understanding, and improving an application. The lists are technology-agnostic unless a concept is inherently tied to a specific technology.

> **How to use this document:** Treat these topics as a reference and assessment checklist, not as a requirement to implement every item. Select concepts according to the application's architecture, business requirements, risk profile, current technology stack, and expected scale. Prefer a simple, maintainable solution over unnecessary infrastructure or abstraction.

---

# 1. Backend Concepts

## 1.1 Backend Architecture and Design Patterns

- Client-server architecture
- Monolithic architecture
- Modular monolith architecture
- Microservices architecture (conceptual awareness; not a default recommendation)
- Layered architecture (presentation, application, domain, infrastructure)
- Model-View-Controller (MVC)
- Model-View-Presenter (MVP)
- Model-View-ViewModel (MVVM)
- Clean Architecture
- Hexagonal Architecture (Ports and Adapters)
- Onion Architecture
- Domain-Driven Design (DDD)
- Domain model and domain services
- Bounded contexts and ubiquitous language
- Separation of concerns
- Single Responsibility Principle (SRP)
- SOLID principles
- Don't Repeat Yourself (DRY)
- Keep It Simple, Stupid (KISS)
- You Aren't Gonna Need It (YAGNI)
- Dependency Injection (DI)
- Inversion of Control (IoC)
- Service layer
- Repository pattern
- Unit of Work pattern
- Data Transfer Objects (DTOs)
- Data mapper pattern
- Active Record pattern
- Factory pattern
- Strategy pattern
- Adapter pattern
- Facade pattern
- Observer pattern
- Decorator pattern
- Command pattern
- Specification pattern
- Middleware and pipeline patterns
- Event-driven architecture
- Domain events and application events
- Synchronous vs. asynchronous processing
- Coupling and cohesion
- Composition over inheritance
- Configuration management and environment-specific configuration
- Feature flags
- Backward compatibility and change management

## 1.2 Database Architecture and Management

- Relational database concepts
- Database schemas and data types
- Entity-Relationship Diagrams (ERD)
- Entities, attributes, and relationships
- Primary keys and foreign keys
- One-to-one, one-to-many, and many-to-many relationships
- Referential integrity
- Database normalization (1NF, 2NF, 3NF, BCNF)
- Denormalization and trade-offs
- Unique, check, not-null, and foreign-key constraints
- Indexes and composite indexes
- Covering indexes and index selectivity
- Query planning and execution plans
- SQL fundamentals and advanced querying
- Joins, subqueries, common table expressions (CTEs)
- Aggregation and window functions
- Views and materialized views
- Stored procedures and triggers (when appropriate)
- Transactions and ACID properties
- Transaction isolation levels
- Dirty reads, non-repeatable reads, and phantom reads
- Optimistic and pessimistic locking
- Row-level and table-level locking
- Deadlocks and deadlock prevention
- Concurrency control
- Atomic database operations
- Database migrations and schema versioning
- Seeders and test data
- Database backups and restoration
- Point-in-time recovery (where supported)
- Data retention and archival
- Data integrity and consistency
- Database connection management and connection pooling
- Query optimization
- N+1 query problem
- Eager loading and lazy loading
- Pagination at database level
- Database performance monitoring
- Relational vs. non-relational databases
- Choosing a database based on data model and workload

## 1.3 API Development and Communication

- API design principles
- REST architectural constraints
- RESTful resource modelling
- HTTP methods (GET, POST, PUT, PATCH, DELETE)
- HTTP status codes
- HTTP headers and content negotiation
- Request and response formats (JSON, form data, multipart data)
- URL and route design
- API versioning
- OpenAPI specification
- Swagger documentation and interactive API documentation
- Request validation
- Response serialization and transformation
- Consistent response envelopes
- Standardized error responses
- Exception-to-HTTP response mapping
- Authentication and authorization in APIs
- API keys and bearer tokens
- Rate limiting and throttling
- Pagination, filtering, sorting, and searching
- Idempotent API operations
- Retry-safe request design
- Timeouts and retry policies
- Webhooks and webhook signature verification
- WebSockets
- Server-Sent Events (SSE)
- Long polling
- GraphQL fundamentals (when relevant)
- API gateways (conceptual awareness)
- CORS configuration
- API contract testing
- API integration testing
- API deprecation policies
- API usage logging and monitoring

## 1.4 Business Logic and Workflow Design

- Separation of business logic from controllers and presentation code
- Domain entities and value objects
- Business rules and invariants
- Application services and domain services
- Use cases and application workflows
- State machines and state transitions
- Workflow orchestration
- Validation at multiple boundaries
- Atomic business operations
- Transaction boundaries
- Consistency across related records
- Idempotency keys
- Duplicate request prevention
- Audit trails for important operations
- Approval and review workflows
- Scheduled and time-dependent operations
- Expiration and timeout handling
- Business calendars, dates, and time zones
- Handling partial failures
- Compensating actions and the Saga pattern (where relevant)
- Domain-specific error handling
- Separation of business decisions from infrastructure concerns

## 1.5 Authentication, Authorization, and User Management

- Authentication vs. authorization
- User identity and account lifecycle
- Registration and account activation
- Login and logout flows
- Password reset and account recovery
- Secure password hashing
- Password policy and password reuse considerations
- Multi-Factor Authentication (MFA)
- One-Time Passwords (OTP)
- Time-based One-Time Passwords (TOTP)
- Email and SMS verification (with appropriate risk controls)
- Role-Based Access Control (RBAC)
- Attribute-Based Access Control (ABAC)
- Permission-based access control
- Resource-level authorization
- Ownership checks
- Policies, gates, and authorization middleware
- Least-privilege principle
- Separation of duties
- OAuth 2.0 fundamentals
- OpenID Connect (OIDC)
- JSON Web Tokens (JWT)
- Access tokens and refresh tokens
- Refresh token rotation and revocation
- Token expiration and audience/scope validation
- Session-based authentication
- Account lockout and login throttling
- Brute-force and credential-stuffing protection
- Privilege escalation prevention
- Account enumeration prevention
- Secure account deactivation and deletion
- Administrative account protection

## 1.6 Caching, Queues, and Background Processing

- Caching fundamentals
- Cache-aside pattern
- Read-through and write-through caching
- Write-behind caching
- In-memory caching
- Distributed caching (conceptual awareness)
- Cache keys and cache namespaces
- Time-to-live (TTL)
- Cache invalidation strategies
- Cache consistency and stale data
- Cache stampede and cache penetration
- Background jobs
- Job queues
- Queue workers
- Synchronous vs. asynchronous jobs
- Retry policies and exponential backoff
- Failed-job handling and dead-letter queues
- Job uniqueness and deduplication
- Idempotent background jobs
- Scheduled tasks and task scheduling
- Long-running task handling
- Progress tracking for asynchronous work
- Event dispatching and listeners
- Notifications and email delivery in background jobs

## 1.7 File Management and Data Processing

- File upload and download workflows
- File type and MIME type validation
- File size limits
- File naming and storage conventions
- File metadata management
- Local file storage
- Object storage concepts (when relevant)
- Private vs. public file storage
- Secure file access and download authorization
- File integrity checks and checksums
- Image resizing and processing
- Document processing
- CSV and spreadsheet import/export
- Data parsing and normalization
- Bulk data processing
- Batch operations
- Streaming large files
- Temporary file management
- File retention and deletion policies

## 1.8 Logging, Monitoring, and Error Handling

- Application logging
- Structured logging
- Log levels (debug, info, notice, warning, error, critical)
- Contextual logging and correlation IDs
- Request and transaction tracing
- Centralized exception handling
- Error classification
- Safe error responses
- Avoiding sensitive information in logs
- Application health checks
- Readiness and liveness concepts
- Metrics and performance monitoring
- Response-time monitoring
- Database query monitoring
- Background-job monitoring
- Audit logging
- Alerting and incident response
- Debugging production issues
- Error reporting and aggregation
- Log rotation and retention

## 1.9 Backend Testing and Code Quality

- Unit testing
- Integration testing
- Feature testing
- API testing
- End-to-end testing
- Contract testing
- Regression testing
- Smoke testing
- Acceptance testing
- Database testing
- Test fixtures and factories
- Mocking, stubbing, and fakes
- Test doubles and dependency isolation
- Testing transactions and rollback behavior
- Testing authorization boundaries
- Testing validation and error cases
- Testing concurrency-sensitive operations
- Test coverage and coverage limitations
- Static analysis
- Type checking and type safety
- Linting and formatting
- Code review
- Refactoring
- Technical debt management
- Documentation and maintainable code

## 1.10 Performance and Scalability

- Performance profiling
- Identifying bottlenecks
- Algorithmic complexity and Big O notation
- Efficient data structures
- Database query optimization
- Avoiding unnecessary database queries
- Pagination and bounded result sets
- Bulk inserts and updates
- Connection management
- Memory management
- CPU and I/O considerations
- Response compression
- HTTP caching headers
- Application-level caching
- Asynchronous processing
- Concurrency and race conditions
- Horizontal vs. vertical scaling (conceptual awareness)
- Stateless application design
- Load testing
- Stress testing
- Capacity planning
- Graceful degradation
- Resource limits and timeouts
- Performance budgets

---

# 2. Frontend Concepts

## 2.1 Frontend Architecture and Organization

- Component-based architecture
- Modular frontend architecture
- Separation of concerns
- Reusable components
- Component composition
- Component hierarchy and ownership
- Atomic Design methodology
- Feature-based folder structure
- Separation of UI, state, and data-access logic
- Design systems
- Shared component libraries
- Routing and navigation architecture
- Client-Side Rendering (CSR)
- Server-Side Rendering (SSR)
- Static Site Generation (SSG)
- Progressive Web Apps (PWA)
- Progressive enhancement
- Graceful degradation
- Frontend-backend contract alignment
- Environment-specific frontend configuration

## 2.2 HTML and Semantic Structure

- Semantic HTML5
- Document structure and landmarks
- Headings hierarchy
- Sections, articles, navigation, and main content
- Forms and form controls
- Buttons vs. links
- Tables and tabular data
- Lists and grouping elements
- Accessible labels and descriptions
- Native browser validation
- Metadata and document titles
- Proper use of ARIA attributes
- Avoiding unnecessary ARIA
- HTML validity and maintainable markup

## 2.3 CSS and Advanced Styling

- CSS syntax and selectors
- Specificity and cascade
- Inheritance
- Box model
- Display types
- Positioning and stacking contexts
- Flexbox
- CSS Grid
- Responsive units (%, rem, em, vw, vh)
- CSS custom properties (variables)
- Pseudo-classes and pseudo-elements
- Attribute selectors
- Combinators
- Media queries
- Container queries
- Responsive typography
- Fluid layouts
- Min/max/clamp functions
- Overflow and scroll behavior
- Z-index management
- Transitions and animations
- Transformations
- Keyframe animations
- Focus, hover, active, and disabled states
- CSS architecture and naming conventions
- BEM methodology
- Utility classes
- CSS Modules and scoped styling concepts
- Theme variables
- Light and dark themes
- Reduced-motion preferences
- Print styles
- Cross-browser compatibility

## 2.4 JavaScript and Browser APIs

- JavaScript fundamentals
- Variables, scope, and closures
- Functions and higher-order functions
- Objects, arrays, and destructuring
- Classes and prototypes
- ES modules
- Modern ECMAScript features
- DOM manipulation
- DOM traversal
- Event handling
- Event bubbling and capturing
- Event delegation
- Preventing default browser behavior
- Asynchronous JavaScript
- Promises
- async/await
- Error handling with try/catch
- Fetch API
- XMLHttpRequest (legacy awareness)
- JSON parsing and serialization
- FormData
- URL and URLSearchParams
- Browser history API
- Local Storage and Session Storage
- Cookies and cookie attributes
- Clipboard API
- Drag and Drop API
- Intersection Observer
- Mutation Observer
- Resize Observer
- Web Workers (when relevant)
- WebSocket API
- Browser permissions and feature detection
- Debouncing and throttling
- Memory leaks and event-listener cleanup

## 2.5 UI Components and Interaction Design

- Buttons and button variants
- Links and navigation elements
- Text inputs and text areas
- Selects, dropdowns, and autocomplete
- Checkboxes and radio buttons
- Toggle switches
- Date and time pickers
- Search fields
- Form groups and field descriptions
- Tables and data grids
- Sorting and filtering controls
- Pagination controls
- Tabs
- Accordions
- Modals and dialogs
- Drawers and side panels
- Tooltips and popovers
- Breadcrumbs
- Menus and context menus
- Cards and summary panels
- Badges and status indicators
- Toast notifications
- Inline alerts
- Progress indicators
- Loading spinners
- Skeleton screens
- Empty states
- Error states
- Success states
- Confirmation dialogs
- Multi-step forms and wizards
- Keyboard interaction patterns
- Focus management
- Component disabled and read-only states

## 2.6 Forms and User Input

- Form design and structure
- Client-side validation
- Server-side validation as the source of truth
- Required and optional fields
- Input constraints and formatting
- Inline validation feedback
- Validation timing (on input, blur, or submit)
- Error summaries
- Field-level error messages
- Accessible labels and instructions
- Input masks and formatting
- Preventing accidental double submission
- Submit/loading states
- Preserving user input after validation errors
- Multi-step forms
- Conditional fields
- Dependent fields
- File input and upload progress
- Clear distinction between validation errors and system errors

## 2.7 Frontend State Management and Data Flow

- Local component state
- Shared and global state
- Derived state
- State ownership
- Unidirectional data flow
- Props and component communication
- Context and dependency sharing
- State management libraries (when justified)
- Server state vs. client state
- Data fetching and caching
- Optimistic UI updates
- Loading, error, and success states
- Cache invalidation and refetching
- Form state management
- URL state and query parameters
- Persistent state
- State synchronization
- Race conditions in data fetching
- Request cancellation
- Handling stale responses
- Avoiding unnecessary re-renders

## 2.8 UI/UX Design Principles

- User-centered design
- User research and user journeys
- Information architecture
- Visual hierarchy
- Consistency and predictability
- Recognition rather than recall
- Feedback and system status visibility
- Affordances and signifiers
- Clear navigation
- Progressive disclosure
- Error prevention
- Error recovery
- Clear microcopy and labels
- Cognitive load reduction
- Interaction cost and task efficiency
- Empty-state guidance
- Confirmation for destructive actions
- Undo and recovery patterns
- Trust and transparency in interface design
- Usability testing
- Heuristic evaluation

## 2.9 Design Systems and Visual Consistency

- Design tokens
- Color system and semantic colors
- Typography scale
- Spacing scale
- Layout grid
- Border, radius, and shadow conventions
- Iconography
- Component variants
- Component states
- Consistent interaction patterns
- Shared styles and reusable components
- Design documentation
- Design-to-development handoff
- Visual regression testing
- Brand consistency

## 2.10 Responsive Design and Accessibility

- Mobile-first design
- Breakpoints
- Fluid and adaptive layouts
- Responsive navigation
- Touch target sizing
- Touch and pointer interactions
- Keyboard accessibility
- Visible focus indicators
- Screen-reader support
- Semantic HTML
- Accessible names and descriptions
- Color contrast
- Not relying on color alone
- Text resizing and zoom support
- Reduced-motion support
- Accessible forms and error messages
- Accessible dialogs and menus
- WCAG principles (Perceivable, Operable, Understandable, Robust)
- WCAG conformance levels (A, AA, AAA)
- ARIA Authoring Practices
- Testing with keyboard and assistive technologies

## 2.11 Frontend Performance and Optimization

- Measuring real user performance
- Core Web Vitals
- Largest Contentful Paint (LCP)
- Interaction to Next Paint (INP)
- Cumulative Layout Shift (CLS)
- First Contentful Paint (FCP)
- Time to First Byte (TTFB)
- JavaScript bundle size
- Code splitting
- Lazy loading
- Dynamic imports
- Tree shaking
- Minification
- Compression
- Image optimization
- Responsive images
- Font loading optimization
- Preload, preconnect, and prefetch
- Avoiding render-blocking resources
- Reducing unnecessary DOM updates
- Avoiding layout thrashing
- Efficient event handling
- Caching static assets
- Performance profiling
- Network waterfall analysis
- Performance budgets

## 2.12 Data Visualization and Dashboard Design

- Choosing the right chart for the data
- Bar charts and column charts
- Line charts and area charts
- Pie and donut charts (only when appropriate)
- Histograms and distributions
- Tables for precise values
- KPI cards and summary metrics
- Filters and date ranges
- Drill-down and detail views
- Clear labels, units, and legends
- Accessible chart alternatives
- Avoiding misleading scales and visualizations
- Handling missing and incomplete data
- Loading and empty states for dashboards
- Real-time and periodically refreshed data
- Exporting and printing reports

---

# 3. Security Concepts

Security should be considered across the entire application lifecycle and at every layer: frontend, backend, database, infrastructure, integrations, and operational processes. The following topics include general application security and additional considerations for systems that process financial or personal data.

## 3.1 Authentication Security

- Secure registration and login flows
- Secure password storage using modern password-hashing algorithms
- Password reset security
- Account recovery security
- Multi-Factor Authentication (MFA)
- One-Time Passwords (OTP)
- Time-based One-Time Passwords (TOTP)
- Phishing-resistant authentication concepts
- Brute-force protection
- Credential-stuffing protection
- Login throttling and rate limiting
- Account lockout trade-offs
- Account enumeration prevention
- Secure email and phone verification
- Re-authentication for sensitive actions
- Secure administrative authentication
- Account lifecycle and deactivation

## 3.2 Authorization and Access Control Security

- Role-Based Access Control (RBAC)
- Attribute-Based Access Control (ABAC)
- Permission-based access control
- Least-privilege principle
- Deny-by-default approach
- Separation of duties
- Resource ownership validation
- Object-level authorization
- Function-level authorization
- Field-level authorization
- Tenant and organizational boundaries (if applicable)
- Insecure Direct Object Reference (IDOR) prevention
- Broken Object Level Authorization (BOLA) prevention
- Broken Function Level Authorization (BFLA) prevention
- Privilege escalation prevention
- Horizontal and vertical privilege escalation
- Authorization checks on every relevant backend operation
- Avoiding reliance on frontend-only access restrictions
- Periodic permission review

## 3.3 Session and Token Security

- Secure session creation and lifecycle
- Session ID unpredictability
- Session fixation prevention
- Session hijacking prevention
- Session expiration and inactivity timeouts
- Session invalidation on logout
- Session invalidation after password changes or account compromise
- Secure cookie attributes (HttpOnly, Secure, SameSite)
- Cookie scope and path configuration
- Cross-Site Request Forgery (CSRF) protection
- Access token expiration
- Refresh token rotation
- Refresh token reuse detection
- Token revocation
- JWT signature and algorithm validation
- JWT issuer, audience, expiry, and scope validation
- Secure token storage considerations
- Avoiding sensitive tokens in URLs and logs
- OAuth 2.0 and OpenID Connect security considerations

## 3.4 Input Validation and Injection Prevention

- Server-side validation of all untrusted input
- Allowlist-based validation where practical
- Type, length, range, and format validation
- Canonicalization and normalization
- Output encoding
- SQL Injection prevention
- Parameterized queries and prepared statements
- Cross-Site Scripting (XSS) prevention
- Stored, reflected, and DOM-based XSS
- Context-aware output encoding
- Safe HTML sanitization when HTML input is required
- Command Injection prevention
- Path Traversal prevention
- Local and Remote File Inclusion prevention
- Server-Side Request Forgery (SSRF) prevention
- XML External Entity (XXE) prevention
- Server-side template injection prevention
- Header injection prevention
- Mass assignment protection
- Unsafe deserialization prevention
- Regular-expression denial of service (ReDoS) awareness
- Safe handling of rich text and user-generated content

## 3.5 Database Security

- Database least-privilege accounts
- Separate database users and permissions where appropriate
- Parameterized queries
- Secure database configuration
- Database access restricted to required application components
- Protection of database credentials
- Encryption in transit
- Encryption at rest
- Sensitive field encryption where appropriate
- Key management and rotation
- Database backups and backup encryption
- Backup access control
- Secure database migrations
- Audit logging for sensitive data access
- Data minimization
- Data retention and secure deletion
- Avoiding sensitive data in query logs
- Preventing unauthorized bulk exports
- Database integrity constraints
- Transaction safety and concurrency control

## 3.6 Cryptography and Data Protection

- Encryption vs. hashing vs. encoding
- Symmetric encryption
- Asymmetric encryption
- Secure hashing
- Password hashing vs. general-purpose hashing
- Digital signatures
- Message Authentication Codes (MAC/HMAC)
- Secure random number generation
- Key generation, storage, rotation, and revocation
- Key management systems (conceptual awareness)
- TLS and HTTPS
- Certificate validation
- Encryption in transit
- Encryption at rest
- Field-level encryption
- Data masking
- Tokenization
- Pseudonymization
- Anonymization and its limitations
- Personally Identifiable Information (PII) protection
- Data classification
- Data minimization
- Secure deletion and retention policies
- Avoiding custom cryptographic algorithms
- Selecting established cryptographic libraries and algorithms

## 3.7 API Security

- API authentication
- API authorization
- Object-level authorization
- Function-level authorization
- Property-level authorization
- Rate limiting and throttling
- Request size limits
- Input validation
- Output filtering and data minimization
- Secure error responses
- CORS configuration
- CSRF protection where cookie-based authentication is used
- API key protection and rotation
- OAuth 2.0 and OpenID Connect security
- JWT validation
- Webhook signature validation
- Replay attack prevention
- Idempotency keys
- Pagination limits
- Resource consumption limits
- API version and endpoint exposure management
- Inventory of API endpoints
- Secure API documentation and test environments
- Protection against mass assignment
- Protection against excessive data exposure
- API logging without secrets or sensitive payloads

## 3.8 Frontend Security

- Cross-Site Scripting (XSS) prevention
- Content Security Policy (CSP)
- Secure handling of user-generated content
- Safe DOM manipulation
- Avoiding unsafe HTML insertion
- Cross-Site Request Forgery (CSRF) awareness and defenses
- Secure cookie configuration
- Avoiding sensitive data in localStorage and sessionStorage
- Avoiding secrets in frontend source code
- Dependency and third-party script security
- Subresource Integrity (SRI) where applicable
- Clickjacking protection and frame restrictions
- Referrer Policy
- Secure browser permissions
- Open redirect prevention
- Safe URL handling
- External link security
- Sensitive data exposure through browser history, cache, or logs
- Source map exposure considerations
- Frontend route guards as a usability layer, not a substitute for backend authorization
- Secure handling of authentication state

## 3.9 File Upload and Storage Security

- File extension validation
- MIME type validation
- File signature (magic-byte) validation
- File size limits
- Randomized server-side file names
- Path traversal prevention
- Storage outside the public web root when appropriate
- Private file storage
- Authorization before file access
- Malware scanning where appropriate
- Safe image and document processing
- Protection against malicious or malformed files
- Archive extraction safety
- Preventing executable uploads
- Download response headers
- Secure temporary file handling
- File retention and deletion policies
- Avoiding trust in client-provided file metadata

## 3.10 Security Logging, Auditing, and Monitoring

- Security event logging
- Authentication event logging
- Failed login monitoring
- Authorization failure monitoring
- Privileged action auditing
- Audit trails for sensitive data changes
- Financial transaction audit trails
- Log integrity and access control
- Centralized logging concepts
- Correlation IDs and request tracing
- Alerting on suspicious activity
- Anomaly detection concepts
- Incident detection and response
- Security incident documentation
- Log retention policies
- Avoiding passwords, tokens, secrets, and unnecessary personal data in logs
- Time synchronization and reliable timestamps
- Monitoring repeated failures and unusual access patterns

## 3.11 Application Security Architecture and Secure Development Lifecycle

- Security by design
- Secure by default
- Defense in depth
- Threat modeling
- Attack surface analysis
- Trust boundaries and data-flow diagrams
- STRIDE threat modeling methodology
- Risk assessment and risk prioritization
- Secure Software Development Lifecycle (SSDLC)
- Security requirements and acceptance criteria
- Secure coding standards
- Security-focused code review
- Static Application Security Testing (SAST)
- Dynamic Application Security Testing (DAST)
- Software Composition Analysis (SCA)
- Dependency vulnerability scanning
- Secret scanning
- Infrastructure and configuration scanning where relevant
- Penetration testing
- Vulnerability management
- Patch management
- Security regression testing
- Dependency and supply-chain security
- Software Bill of Materials (SBOM) concepts
- Secure environment configuration
- Secrets management
- Incident response planning
- Backup and recovery testing
- OWASP Application Security Verification Standard (ASVS)
- OWASP Top 10
- OWASP API Security Top 10
- OWASP Cheat Sheet Series

## 3.12 Banking and Financial Application Security

These topics are especially relevant to applications that manage bank accounts, balances, payments, transfers, or other financial operations. Their applicability depends on the actual product, business model, jurisdiction, and regulatory scope.

### Financial data and account protection

- Protection of account and financial data
- Confidentiality, integrity, and availability of financial information
- Account takeover prevention
- Strong customer authentication concepts
- Multi-Factor Authentication (MFA)
- Step-up authentication for high-risk operations
- Secure device and session management
- Detection of unusual account activity
- Transaction and account access notifications
- Secure handling of account identifiers
- Data minimization and masking of financial information

### Transaction integrity and correctness

- Transaction authorization
- Transaction confirmation and review
- Transaction signing concepts
- Transaction integrity
- Atomic financial operations
- Database transaction boundaries
- Idempotency and duplicate transaction prevention
- Replay attack prevention
- Double-spending prevention
- Race-condition prevention
- Concurrency control for balance updates
- Optimistic and pessimistic locking
- Consistent balance calculations
- Ledger integrity
- Immutable or tamper-evident transaction records
- Double-entry bookkeeping concepts
- Reconciliation processes
- Reversal and correction workflows
- Transaction status state machines
- Pending, completed, rejected, reversed, and failed transaction states
- Reliable handling of timeouts and uncertain payment outcomes
- Exactly-once processing limitations and practical idempotent processing

### Fraud and abuse prevention

- Fraud detection concepts
- Transaction velocity checks
- Transaction limits
- Risk-based authentication
- Behavioral and anomaly detection
- Suspicious activity monitoring
- Account takeover detection
- Device and session risk signals
- Duplicate and unusual transaction detection
- Beneficiary and recipient validation
- Cooling-off periods for sensitive changes (where appropriate)
- Secure transaction notifications
- Manual review and escalation workflows

### Payments and external integrations

- Payment gateway security
- Payment processor integration security
- Secure webhook handling
- Signature verification for payment notifications
- Idempotent payment requests
- Reconciliation with external providers
- Secure handling of payment references
- Tokenization of payment card data
- PCI DSS concepts and scope
- Secure communication with banking and payment APIs
- Third-party risk management
- Failure, timeout, retry, and duplicate-notification handling

### Regulatory and compliance awareness

- Know Your Customer (KYC) concepts
- Anti-Money Laundering (AML) concepts
- Strong Customer Authentication (SCA), where applicable
- Payment Services Directive (PSD2) and related requirements, where applicable
- General Data Protection Regulation (GDPR) and applicable local data-protection rules
- PCI DSS when the application stores, processes, or transmits payment card data or otherwise falls within its scope
- Auditability and record retention requirements
- Separation of duties and privileged access controls
- Regulatory reporting and evidence preservation, where applicable

> **Important:** PCI DSS, AML/KYC, SCA, PSD2, and other regulatory requirements are not automatically applicable to every application described as a banking application. Applicability must be established from the system's actual functions, data flows, payment-card involvement, jurisdiction, and role in the financial ecosystem. A production banking system requires review against the relevant legal, regulatory, and institutional requirements by qualified specialists.

## 3.13 Security Considerations for a Library Management Application

The following topics are particularly relevant to a library application that manages members, librarians, administrators, books, copies, reservations, loans, and member information.

### User accounts and roles

- Secure authentication for members, librarians, and administrators
- Role-Based Access Control (RBAC)
- Least-privilege permissions for each role
- Server-side authorization for every protected action
- Prevention of privilege escalation
- Protection of administrative accounts
- Secure password reset and account recovery
- Session expiration and secure logout
- Account activation, suspension, and deactivation
- Audit trail for role and permission changes

### Member privacy and personal data

- Data minimization for member profiles
- Restricting access to personal information
- Preventing members from viewing another member's profile, loans, or reservations
- Masking sensitive information in administrative interfaces where appropriate
- Secure data export and reporting
- Retention and deletion rules for member data
- Protection of contact details and account information
- Avoiding personal data in application logs

### Reservations, loans, and copy availability

- Server-side validation of reservation eligibility
- Authorization checks for reservation creation and cancellation
- Atomic updates to reservation and copy status
- Prevention of two members reserving or borrowing the same copy simultaneously
- Database transactions for loan and reservation workflows
- Concurrency control and locking where needed
- Valid state transitions for reservation and loan statuses
- Prevention of unauthorized status changes
- Validation of pickup deadlines and expiration rules
- Idempotency or duplicate-submission protection for critical actions
- Audit logging for approvals, rejections, pickups, returns, and cancellations
- Consistency between copy availability and active loans/reservations

### Administrative operations and catalogue management

- Restricting book, copy, genre, and member management to authorized roles
- Validation of imported catalogue data
- Safe CSV and spreadsheet imports
- Protection against formula injection in spreadsheet exports
- Validation of bulk operations
- Audit trails for catalogue and member changes
- Confirmation and recovery mechanisms for destructive operations
- Protection against accidental or unauthorized bulk deletion

### AI chatbot and external services, if present

- Treating user messages and retrieved documents as untrusted input
- Prompt injection awareness and mitigation
- Preventing the chatbot from bypassing application authorization
- Enforcing member-specific data access in backend services
- Preventing disclosure of another member's reservations, loans, or profile data
- Separating public catalogue information from private account information
- Validating and limiting tool/function calls made by the assistant
- Avoiding secrets and personal data in prompts and logs
- Rate limiting and abuse protection
- Safe fallback behavior when the AI service is unavailable
- Logging AI interactions with appropriate privacy controls
- Evaluating hallucination and incorrect-answer risks
- Grounding catalogue answers in authoritative database records
- Keeping business-critical operations deterministic and backend-controlled
- Treating vector search results and retrieved content as data, not executable instructions
- Securing communication between the main application and AI microservices

---

# 4. Cross-Cutting Engineering Practices

The following practices apply across backend, frontend, and security work.

## 4.1 Maintainability and Technical Quality

- Readability and consistency
- Clear naming conventions
- Small, focused functions and classes
- Separation of concerns
- Avoiding unnecessary complexity
- Reusable abstractions only where they provide real value
- Explicit contracts between modules
- Documentation of non-obvious decisions
- Refactoring and technical debt management
- Consistent error-handling conventions
- Dependency management
- Version control and meaningful commits
- Code review and peer review

## 4.2 Reliability and Resilience

- Defensive programming
- Input and output boundary validation
- Timeouts for external operations
- Retry policies with backoff
- Idempotent operations
- Graceful handling of unavailable dependencies
- Transactional consistency
- Recovery from partial failures
- Backup and restore procedures
- Data integrity checks
- Health checks
- Graceful degradation
- Clear user-facing failure states

## 4.3 Documentation and Technical Communication

- Architecture overview
- Database schema documentation
- API documentation
- Setup and configuration instructions
- Environment variable documentation
- Deployment and maintenance notes
- Security assumptions and known limitations
- Decision records (Architecture Decision Records, ADRs)
- User and administrator documentation

## 4.4 Practical Assessment Priorities

When assessing an existing application, a useful order is:

1. Understand the current architecture, technology stack, data model, and business workflows.
2. Identify correctness issues in critical business operations.
3. Review authentication, authorization, privacy, and input validation.
4. Review database integrity, transactions, and concurrency behavior.
5. Review error handling, logging, and auditability.
6. Review usability, accessibility, responsive behavior, and interface consistency.
7. Measure performance before introducing optimization or caching.
8. Identify maintainability issues and refactor incrementally.
9. Consider scaling mechanisms only when requirements or measurements justify them.
10. Verify changes with automated tests and targeted security checks.

---

# 5. Reference Standards and Documentation

Use primary documentation and established security standards when researching or validating an individual concept.

## Security

- [OWASP Application Security Verification Standard (ASVS)](https://asvs.owasp.org/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)

## Frontend and Web Platform

- [MDN Web Docs](https://developer.mozilla.org/en-US/docs/Web)
- [W3C Web Accessibility Initiative (WAI)](https://www.w3.org/WAI/)
- [W3C Web Content Accessibility Guidelines (WCAG)](https://www.w3.org/WAI/standards-guidelines/wcag/)
- [web.dev](https://web.dev/learn/)

## Backend and Data

- [PHP Documentation](https://www.php.net/manual/en/)
- [Laravel Documentation](https://laravel.com/docs/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)

---

# 6. Guidance for AI-Assisted Code Review

When using this document with an AI coding assistant, request analysis before implementation. The assistant should:

- Inspect the existing codebase and identify the actual framework, language, database, and architectural conventions.
- Map relevant concepts from this document to concrete files, classes, routes, components, database tables, and workflows.
- Distinguish confirmed issues from potential risks and general recommendations.
- Explain the impact, severity, and practical reproduction scenario for each finding.
- Prioritize security vulnerabilities, data integrity, authorization, and business-logic correctness.
- Avoid proposing infrastructure or architectural complexity without a demonstrated need.
- Preserve existing behavior unless a change is explicitly required.
- Recommend incremental changes compatible with the current stack.
- Include tests or verification steps for proposed changes.
- Never claim that an application is secure solely because a checklist has been reviewed.
- Flag matters that require specialist, legal, regulatory, or compliance review.
