package com.rubberjam.codebinder;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import com.rubberjam.codebinder.CodeBinderMojo;

import java.io.File;
import java.nio.file.Path;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;

/**
 * Unit tests for {@link CodeBinderMojo}.
 */
class CodeBinderMojoTest {

    @TempDir
    Path tempDir;

    @Test
    void testBuildCommand_withProjectFile() throws Exception {
        var mojo = new CodeBinderMojo();

        // Use reflection to set private fields for testing
        setField(mojo, "projectFile", new File("C:/test/MyProject.csproj"));
        setField(mojo, "outputDirectory", new File("C:/output/java"));
        setField(mojo, "namespaceMappings", java.util.Map.of("MyNamespace", "com.my.namespace"));
        setField(mojo, "dotnetExecutable", "dotnet");
        setField(mojo, "android", false);

        Path fakeCodeBinderProject = Path.of("C:/CodeBinder/CodeBinder/CodeBinder.csproj");
        List<String> command = mojo.buildCommand(fakeCodeBinderProject);

        assertTrue(command.contains("dotnet"));
        assertTrue(command.contains("run"));
        assertTrue(command.contains("--language=Java"));
        assertTrue(command.contains("--project=C:\\test\\MyProject.csproj"));
        assertTrue(command.stream().anyMatch(s -> s.startsWith("--targetpath=")));
        assertTrue(command.stream().anyMatch(s -> s.startsWith("--nsmapping=MyNamespace:com.my.namespace")));
        assertFalse(command.contains("--android"));
    }

    @Test
    void testBuildCommand_withSolutionAndProject() throws Exception {
        var mojo = new CodeBinderMojo();

        setField(mojo, "solutionFile", new File("C:/test/MySolution.sln"));
        setField(mojo, "projectName", "GameState");
        setField(mojo, "outputDirectory", new File("C:/output/java"));
        setField(mojo, "namespaceMappings", java.util.Map.of("GameState", "com.game.state"));
        setField(mojo, "dotnetExecutable", "dotnet");
        setField(mojo, "android", false);

        Path fakeCodeBinderProject = Path.of("C:/CodeBinder/CodeBinder/CodeBinder.csproj");
        List<String> command = mojo.buildCommand(fakeCodeBinderProject);

        assertTrue(command.contains("--solution=C:\\test\\MySolution.sln"));
        assertTrue(command.contains("--project=GameState"));
        assertTrue(command.contains("--language=Java"));
    }

    @Test
    void testBuildCommand_withAndroidFlag() throws Exception {
        var mojo = new CodeBinderMojo();

        setField(mojo, "projectFile", new File("C:/test/MyProject.csproj"));
        setField(mojo, "outputDirectory", new File("C:/output/java"));
        setField(mojo, "namespaceMappings", java.util.Map.of("NS", "com.ns"));
        setField(mojo, "dotnetExecutable", "dotnet");
        setField(mojo, "android", true);

        Path fakeCodeBinderProject = Path.of("C:/CodeBinder/CodeBinder/CodeBinder.csproj");
        List<String> command = mojo.buildCommand(fakeCodeBinderProject);

        assertTrue(command.contains("--android"));
    }

    @Test
    void testBuildCommand_withMsbuildProperties() throws Exception {
        var mojo = new CodeBinderMojo();

        setField(mojo, "projectFile", new File("C:/test/MyProject.csproj"));
        setField(mojo, "outputDirectory", new File("C:/output/java"));
        setField(mojo, "namespaceMappings", java.util.Map.of("NS", "com.ns"));
        setField(mojo, "dotnetExecutable", "dotnet");
        setField(mojo, "android", false);
        setField(mojo, "msbuildProperties", java.util.Map.of("Configuration", "Release"));

        Path fakeCodeBinderProject = Path.of("C:/CodeBinder/CodeBinder/CodeBinder.csproj");
        List<String> command = mojo.buildCommand(fakeCodeBinderProject);

        assertTrue(command.stream().anyMatch(s -> s.equals("--property=Configuration:Release")));
    }

    @Test
    void testResolveCodeBinderProject_withExplicitPath() throws Exception {
        // Create a fake CodeBinder.csproj in temp dir
        Path codeBinderDir = tempDir.resolve("CodeBinder");
        java.nio.file.Files.createDirectories(codeBinderDir);
        Path csproj = codeBinderDir.resolve("CodeBinder.csproj");
        java.nio.file.Files.writeString(csproj, "<Project/>");

        var mojo = new CodeBinderMojo();
        setField(mojo, "codeBinderPath", tempDir.toFile());

        // Need to set a mavenProject too
        var project = new org.apache.maven.project.MavenProject();
        project.setFile(tempDir.resolve("pom.xml").toFile());
        setField(mojo, "mavenProject", project);

        Path resolved = mojo.resolveCodeBinderProject();
        assertEquals(csproj.toAbsolutePath(), resolved.toAbsolutePath());
    }

    private void setField(Object target, String fieldName, Object value) throws Exception {
        var field = target.getClass().getDeclaredField(fieldName);
        field.setAccessible(true);
        field.set(target, value);
    }
}
