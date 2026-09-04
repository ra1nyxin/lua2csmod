using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lua2CS;

public sealed record LuaScriptValidationResult(
    string Path,
    string Key,
    string Name,
    string Version,
    int RegistrationCount,
    IReadOnlyList<string> Commands,
    string? Error)
{
    public bool Success => Error is null;

    internal static LuaScriptValidationResult Failure(string path, string error) => new(
        path,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        [],
        error);
}

/// <summary>在不连接 CS2 服务端的情况下预检 Lua2CS 脚本。</summary>
public static class LuaScriptValidator
{
    public static IReadOnlyList<LuaScriptValidationResult> Validate(
        string path,
        bool allowUnsafeLibraries = false,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        logger ??= NullLogger.Instance;

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return [ValidateFile(fullPath, allowUnsafeLibraries, logger)];
        }

        if (!Directory.Exists(fullPath))
        {
            return [LuaScriptValidationResult.Failure(fullPath, "路径不存在。")];
        }

        var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
            .Where(LuaPluginManager.IsStandaloneScript)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
        {
            return [LuaScriptValidationResult.Failure(fullPath, "目录中没有可独立加载的顶层 Lua 脚本。")];
        }

        var results = files
            .Select(file => ValidateFile(file, allowUnsafeLibraries, logger))
            .ToArray();
        ReportDuplicateCommands(results);
        return results;
    }

    private static LuaScriptValidationResult ValidateFile(string path, bool allowUnsafeLibraries, ILogger logger)
    {
        if (!LuaPluginManager.IsStandaloneScript(path))
        {
            return LuaScriptValidationResult.Failure(path, "只允许校验不以下划线开头的顶层 .lua 脚本。") with
            {
                Key = Path.GetFileNameWithoutExtension(path)
            };
        }

        try
        {
            using var plugin = new LuaRuntime(logger, allowUnsafeLibraries).Prepare(path);
            LuaRegistrationValidator.Validate(plugin.Registrations);
            return new LuaScriptValidationResult(
                path,
                plugin.Key,
                plugin.Name,
                plugin.Version,
                plugin.Registrations.Count,
                plugin.Registrations.OfType<CommandRegistration>()
                    .Select(command => command.Name)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                null);
        }
        catch (Exception exception)
        {
            return LuaScriptValidationResult.Failure(path, Unwrap(exception).Message) with
            {
                Key = Path.GetFileNameWithoutExtension(path)
            };
        }
    }

    private static void ReportDuplicateCommands(LuaScriptValidationResult[] results)
    {
        var duplicateKeys = results
            .Where(result => result.Success)
            .SelectMany(result => result.Commands.Select(command => (Command: command, result.Key)))
            .GroupBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any())
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        if (duplicateKeys.Count == 0) return;

        for (var index = 0; index < results.Length; index++)
        {
            var conflicts = results[index].Commands
                .Where(duplicateKeys.ContainsKey)
                .Select(command => $"{command}（{string.Join("、", duplicateKeys[command])}）")
                .ToArray();
            if (conflicts.Length == 0) continue;
            results[index] = results[index] with
            {
                Error = $"Lua 自定义命令与同目录脚本冲突：{string.Join("；", conflicts)}。"
            };
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is System.Reflection.TargetInvocationException { InnerException: not null })
        {
            exception = exception.InnerException;
        }
        return exception;
    }
}
