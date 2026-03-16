# Maven Plugin & C# Language Extensions for Unity → Java

Build a Maven plugin that wraps the CodeBinder CLI to convert C# source to Java, then incrementally extend the C# → Java conversion to support Unity game state patterns, targeting **Java 23+**.

## Proposed Changes

### Maven Plugin Project

#### [DONE] [pom.xml](file:///c:/Users/Dave/git/CodeBinder/codebinder-maven-plugin/pom.xml)
Maven plugin project (`maven-plugin` packaging) with:
- `maven-plugin-api` + `maven-plugin-annotations` dependencies
- JUnit 5 + `maven-plugin-testing-harness` for integration tests
- Java 23 source/target

#### [DONE] [CodeBinderMojo.java](file:///c:/Users/Dave/git/CodeBinder/codebinder-maven-plugin/src/main/java/com/codebinder/maven/CodeBinderMojo.java)
The `@Mojo(name = "generate")` that:
- Accepts configuration: `sourceDirectory`, `outputDirectory`, `namespaceMappings`, `projectFile`, `solutionFile`
- Locates dotnet SDK and the CodeBinder CLI project
- Invokes `dotnet run --project <CodeBinder.csproj>` with appropriate args
- Adds generated Java sources to the Maven compilation source roots
- Handles error reporting from CodeBinder stderr

#### [DONE] [CodeBinderMojoTest.java](file:///c:/Users/Dave/git/CodeBinder/codebinder-maven-plugin/src/test/java/com/codebinder/maven/CodeBinderMojoTest.java)
Integration test verifying the plugin invokes CodeBinder and produces Java output.

---

### Unity Game State Test Project

#### [DONE] [UnityGameState.csproj](file:///c:/Users/Dave/git/CodeBinder/Test/UnityGameState/UnityGameState.csproj)
Minimal .NET project with C# game state classes — no Unity runtime dependency.

#### [DONE] [GameState.cs](file:///c:/Users/Dave/git/CodeBinder/Test/UnityGameState/GameState.cs)
Representative game state logic using:
- Enums (player state, item types)
- Classes with properties and methods
- `List<>`, `Dictionary<>` collections
- Delegates (`Action<>`, `Func<>`)
- Lambdas and events
- Null-conditional, null-coalescing, string interpolation
- `record` types (where possible)

#### [NEW] [TestSolution.sln](file:///c:/Users/Dave/git/CodeBinder/Test/UnityGameState/UnityGameState.sln)
Solution file referencing the test project and CodeBinder.Redist.

---

### C# Language Extensions (CodeBinder.Java)

#### [MODIFY] [JavaExtensions_Types.cs](file:///c:/Users/Dave/git/CodeBinder/CodeBinder.Java/Java/Extensions/JavaExtensions_Types.cs)
- Add known type mappings for `Dictionary<,>` → `HashMap`, `HashSet<>` → `HashSet`, `Queue<>` → `ArrayDeque`, `Stack<>` → `ArrayDeque`, `StringBuilder` → `StringBuilder`
- Map `Action<>` / `Func<>` delegate types to `java.util.function.*` interfaces (`Consumer`, `Supplier`, `Function`, `BiFunction`, `Predicate`, etc.)

#### [MODIFY] [JavaBuilderExtensions_Expressions.cs](file:///c:/Users/Dave/git/CodeBinder/CodeBinder.Java/Java/Builders/JavaBuilderExtensions_Expressions.cs)
- Add `SimpleLambdaExpression` + `ParenthesizedLambdaExpression` → Java `(args) -> { body }` or `(args) -> expr`
- Add `ConditionalAccessExpression` (`?.`) → null-check ternary `(x != null ? x.y : null)`
- Add `CoalesceExpression` (`??`) → `Objects.requireNonNullElse(a, b)` or ternary
- Add `InterpolatedStringExpression` → `String.format(...)` or concatenation
- Add `DefaultExpression` / `DefaultLiteralExpression` → appropriate Java defaults

#### [MODIFY] [JavaBuilderExtensions_Statements.cs](file:///c:/Users/Dave/git/CodeBinder/CodeBinder.Java/Java/Builders/JavaBuilderExtensions_Statements.cs)
- Review and ensure `using` statement converts cleanly to try-with-resources

#### [MODIFY] [JavaExtensions.cs](file:///c:/Users/Dave/git/CodeBinder/CodeBinder.Java/Java/Extensions/JavaExtensions.cs)
- Add Java operator/keyword mappings for new expression types
- Add string interpolation formatting helpers

#### [MODIFY] [ConversionCSharpToJava.cs](file:///c:/Users/Dave/git/CodeBinder/CodeBinder.Java/ConversionCSharpToJava.cs)
- Add delegate conversions: yield Java functional interfaces for C# delegate declarations
- Add `JavaVersion` property (targeting 23+) to enable modern Java features

---

## Verification Plan

### Automated Tests

1. **Maven plugin build + integration test:**
   ```
   cd codebinder-maven-plugin
   mvn clean install
   ```
   This builds the plugin and runs `CodeBinderMojoTest` which verifies CLI invocation.

2. **CodeBinder codegen test (existing pipeline):**
   ```powershell
   # Build the solution first
   dotnet build CodeBinder.sln --configuration Release /p:Platform="Any CPU"
   
   # Run codegen against the new Unity test project
   .\bin\Release\CodeBinder.exe --solution=Test\UnityGameState\UnityGameState.sln --project=UnityGameState --language=Java --nsmapping=UnityGameState:com.game.state --targetpath=..\CodeBinder-TestCodeGen\UnityGameStateJava
   ```

3. **Java compilation of generated output:**
   ```
   # Compile the generated Java files to verify they are syntactically valid Java 23
   javac --release 23 -d /tmp/codebinder-test <generated-java-files>
   ```
   This confirms the generated Java is valid and compiles.

4. **Per-feature validation:**
   For each language extension, add a corresponding C# construct to `GameState.cs`, run CodeBinder, and verify:
   - The generated Java compiles with `javac --release 23`
   - The generated Java is idiomatic (manual review of output)

### Manual Verification
- Review generated Java output files for idiomatic Java 23 patterns
- Verify Maven plugin `pom.xml` configuration is clean and well-documented
