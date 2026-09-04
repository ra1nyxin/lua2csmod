using Lua2CS;

var arguments = args.ToList();
var allowUnsafeLibraries = arguments.Remove("--allow-unsafe-libraries");
if (arguments.Count != 1)
{
    Console.Error.WriteLine("用法：dotnet run --project tools/Lua2CS.Validate -- [--allow-unsafe-libraries] <Lua 文件或目录>");
    return 64;
}

var results = LuaScriptValidator.Validate(arguments[0], allowUnsafeLibraries);
foreach (var result in results)
{
    if (result.Success)
    {
        var commands = result.Commands.Count == 0 ? "无自定义命令" : string.Join("、", result.Commands);
        Console.WriteLine($"[通过] {result.Key}: {result.Name} v{result.Version}，{result.RegistrationCount} 个注册项，{commands}");
    }
    else
    {
        Console.Error.WriteLine($"[失败] {result.Path}: {result.Error}");
    }
}

return results.All(result => result.Success) ? 0 : 1;
