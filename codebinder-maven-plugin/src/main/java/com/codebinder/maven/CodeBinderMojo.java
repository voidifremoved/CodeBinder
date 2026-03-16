package com.codebinder.maven;

import org.apache.maven.plugin.AbstractMojo;
import org.apache.maven.plugin.MojoExecutionException;
import org.apache.maven.plugin.MojoFailureException;
import org.apache.maven.plugins.annotations.LifecyclePhase;
import org.apache.maven.plugins.annotations.Mojo;
import org.apache.maven.plugins.annotations.Parameter;
import org.apache.maven.project.MavenProject;

import java.io.BufferedReader;
import java.io.File;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.concurrent.TimeUnit;

/**
 * Maven plugin that transpiles C# source code to Java using the CodeBinder CLI.
 * <p>
 * This plugin invokes the .NET CodeBinder tool to convert C# projects into Java source files,
 * enabling shared game-state logic between Unity (C#) and server-side Java.
 * </p>
 *
 * <h3>Example configuration:</h3>
 * <pre>{@code
 * <plugin>
 *     <groupId>com.codebinder</groupId>
 *     <artifactId>codebinder-maven-plugin</artifactId>
 *     <version>1.0.0-SNAPSHOT</version>
 *     <executions>
 *         <execution>
 *             <goals>
 *                 <goal>generate</goal>
 *             </goals>
 *         </execution>
 *     </executions>
 *     <configuration>
 *         <projectFile>${project.basedir}/../GameState/GameState.csproj</projectFile>
 *         <namespaceMappings>
 *             <GameState>com.game.state</GameState>
 *         </namespaceMappings>
 *     </configuration>
 * </plugin>
 * }</pre>
 */
@Mojo(name = "generate", defaultPhase = LifecyclePhase.GENERATE_SOURCES)
public class CodeBinderMojo extends AbstractMojo {

    /**
     * The C# project file (.csproj) to convert.
     * Either this or {@code solutionFile} must be specified.
     */
    @Parameter(property = "codebinder.projectFile")
    private File projectFile;

    /**
     * The C# solution file (.sln) to convert.
     * Either this or {@code projectFile} must be specified.
     */
    @Parameter(property = "codebinder.solutionFile")
    private File solutionFile;

    /**
     * Specific project name to convert within the solution.
     * Only used when {@code solutionFile} is specified.
     */
    @Parameter(property = "codebinder.projectName")
    private String projectName;

    /**
     * Output directory for generated Java source files.
     * Defaults to {@code ${project.build.directory}/generated-sources/codebinder}.
     */
    @Parameter(
            property = "codebinder.outputDirectory",
            defaultValue = "${project.build.directory}/generated-sources/codebinder"
    )
    private File outputDirectory;

    /**
     * Namespace mappings from C# namespace to Java package.
     * Each entry maps a C# namespace (key) to a Java package (value).
     * <p>Example: {@code <GameState>com.game.state</GameState>}</p>
     */
    @Parameter(property = "codebinder.namespaceMappings", required = true)
    private Map<String, String> namespaceMappings;

    /**
     * Path to the CodeBinder project directory.
     * If not specified, the plugin will look for it relative to the current project
     * at {@code ../CodeBinder} or check the CODEBINDER_HOME environment variable.
     */
    @Parameter(property = "codebinder.codeBinderPath")
    private File codeBinderPath;

    /**
     * The dotnet executable to use. Defaults to "dotnet" (found on PATH).
     */
    @Parameter(property = "codebinder.dotnetExecutable", defaultValue = "dotnet")
    private String dotnetExecutable;

    /**
     * Whether to use the Android-compatible output.
     */
    @Parameter(property = "codebinder.android", defaultValue = "false")
    private boolean android;

    /**
     * Additional MSBuild properties to pass to the workspace.
     */
    @Parameter(property = "codebinder.msbuildProperties")
    private Map<String, String> msbuildProperties;

    /**
     * Timeout in seconds for the CodeBinder process.
     */
    @Parameter(property = "codebinder.timeout", defaultValue = "300")
    private int timeout;

    /**
     * Whether to skip code generation.
     */
    @Parameter(property = "codebinder.skip", defaultValue = "false")
    private boolean skip;

    /**
     * The Maven project reference (injected).
     */
    @Parameter(defaultValue = "${project}", readonly = true, required = true)
    private MavenProject mavenProject;

    @Override
    public void execute() throws MojoExecutionException, MojoFailureException {
        if (skip) {
            getLog().info("CodeBinder generation skipped.");
            return;
        }

        validateConfiguration();
        Path codeBinderProject = resolveCodeBinderProject();
        ensureOutputDirectory();

        List<String> command = buildCommand(codeBinderProject);
        executeCodeBinder(command);

        // Add the generated sources to the Maven compilation
        mavenProject.addCompileSourceRoot(outputDirectory.getAbsolutePath());
        getLog().info("Added generated sources: " + outputDirectory.getAbsolutePath());
    }

    private void validateConfiguration() throws MojoExecutionException {
        if (projectFile == null && solutionFile == null) {
            throw new MojoExecutionException(
                    "Either 'projectFile' or 'solutionFile' must be specified in the plugin configuration.");
        }
        if (projectFile != null && !projectFile.exists()) {
            throw new MojoExecutionException("Project file does not exist: " + projectFile.getAbsolutePath());
        }
        if (solutionFile != null && !solutionFile.exists()) {
            throw new MojoExecutionException("Solution file does not exist: " + solutionFile.getAbsolutePath());
        }
        if (namespaceMappings == null || namespaceMappings.isEmpty()) {
            throw new MojoExecutionException("At least one namespace mapping must be specified.");
        }
    }

    /**
     * Resolves the CodeBinder CLI project path.
     * Search order:
     * 1. Explicit {@code codeBinderPath} configuration
     * 2. CODEBINDER_HOME environment variable
     * 3. Relative path {@code ../CodeBinder/CodeBinder/CodeBinder.csproj} from this project
     */
    Path resolveCodeBinderProject() throws MojoExecutionException {
        if (codeBinderPath != null) {
            Path resolved = resolveProjectFile(codeBinderPath.toPath());
            if (resolved != null) return resolved;
            throw new MojoExecutionException("CodeBinder project not found at: " + codeBinderPath);
        }

        // Check environment variable
        String envHome = System.getenv("CODEBINDER_HOME");
        if (envHome != null && !envHome.isBlank()) {
            Path resolved = resolveProjectFile(Path.of(envHome));
            if (resolved != null) return resolved;
            getLog().warn("CODEBINDER_HOME set but CodeBinder.csproj not found at: " + envHome);
        }

        // Try relative paths from the Maven project base directory
        Path baseDir = mavenProject.getBasedir().toPath();
        for (String relative : List.of("../CodeBinder", "../../CodeBinder", "../..")) {
            Path candidate = baseDir.resolve(relative);
            Path resolved = resolveProjectFile(candidate);
            if (resolved != null) return resolved;
        }

        throw new MojoExecutionException(
                "Could not locate CodeBinder. Set 'codeBinderPath' in plugin config or CODEBINDER_HOME env variable.");
    }

    private Path resolveProjectFile(Path basePath) {
        // Direct .csproj reference
        if (basePath.toString().endsWith(".csproj") && Files.exists(basePath)) {
            return basePath;
        }
        // Look for CodeBinder/CodeBinder.csproj under the path
        Path csproj = basePath.resolve("CodeBinder").resolve("CodeBinder.csproj");
        if (Files.exists(csproj)) return csproj;
        // Maybe the path IS the CodeBinder directory
        csproj = basePath.resolve("CodeBinder.csproj");
        if (Files.exists(csproj)) return csproj;
        return null;
    }

    private void ensureOutputDirectory() throws MojoExecutionException {
        if (!outputDirectory.exists() && !outputDirectory.mkdirs()) {
            throw new MojoExecutionException("Could not create output directory: " + outputDirectory);
        }
    }

    List<String> buildCommand(Path codeBinderProject) {
        var command = new ArrayList<String>();
        command.add(dotnetExecutable);
        command.add("run");
        command.add("--project");
        command.add(codeBinderProject.toAbsolutePath().toString());
        command.add("--configuration");
        command.add("Release");
        command.add("--");

        // Source project/solution
        if (solutionFile != null) {
            command.add("--solution=" + solutionFile.getAbsolutePath());
            if (projectName != null && !projectName.isBlank()) {
                command.add("--project=" + projectName);
            }
        } else {
            command.add("--project=" + projectFile.getAbsolutePath());
        }

        // Target language
        command.add("--language=Java");

        // Android switch
        if (android) {
            command.add("--android");
        }

        // Output directory
        command.add("--targetpath=" + outputDirectory.getAbsolutePath());

        // Namespace mappings
        for (var entry : namespaceMappings.entrySet()) {
            command.add("--nsmapping=" + entry.getKey() + ":" + entry.getValue());
        }

        // MSBuild properties
        if (msbuildProperties != null) {
            for (var entry : msbuildProperties.entrySet()) {
                command.add("--property=" + entry.getKey() + ":" + entry.getValue());
            }
        }

        return command;
    }

    private void executeCodeBinder(List<String> command) throws MojoExecutionException {
        getLog().info("Executing CodeBinder: " + String.join(" ", command));

        try {
            var processBuilder = new ProcessBuilder(command);
            processBuilder.redirectErrorStream(false);
            processBuilder.environment().putAll(System.getenv());

            Process process = processBuilder.start();

            // Capture stdout and stderr in parallel
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            Thread stdoutThread = Thread.ofVirtual().start(() -> {
                try (var reader = new BufferedReader(new InputStreamReader(process.getInputStream()))) {
                    String line;
                    while ((line = reader.readLine()) != null) {
                        stdout.append(line).append(System.lineSeparator());
                        getLog().info("[CodeBinder] " + line);
                    }
                } catch (IOException e) {
                    getLog().warn("Error reading CodeBinder stdout", e);
                }
            });

            Thread stderrThread = Thread.ofVirtual().start(() -> {
                try (var reader = new BufferedReader(new InputStreamReader(process.getErrorStream()))) {
                    String line;
                    while ((line = reader.readLine()) != null) {
                        stderr.append(line).append(System.lineSeparator());
                        getLog().warn("[CodeBinder] " + line);
                    }
                } catch (IOException e) {
                    getLog().warn("Error reading CodeBinder stderr", e);
                }
            });

            boolean finished = process.waitFor(timeout, TimeUnit.SECONDS);
            stdoutThread.join(5000);
            stderrThread.join(5000);

            if (!finished) {
                process.destroyForcibly();
                throw new MojoExecutionException("CodeBinder process timed out after " + timeout + " seconds.");
            }

            int exitCode = process.exitValue();
            if (exitCode != 0) {
                throw new MojoExecutionException(
                        "CodeBinder failed with exit code " + exitCode + ".\n" +
                                "stderr: " + stderr + "\n" +
                                "stdout: " + stdout);
            }

            getLog().info("CodeBinder completed successfully. Generated Java sources in: " + outputDirectory);

        } catch (IOException e) {
            throw new MojoExecutionException("Failed to execute CodeBinder. Is 'dotnet' installed and on PATH?", e);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new MojoExecutionException("CodeBinder execution interrupted.", e);
        }
    }
}
