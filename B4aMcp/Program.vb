Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports ModelContextProtocol.Server

Module Program
    Sub Main(args As String())
        RunAsync(args).GetAwaiter().GetResult()
    End Sub

    Async Function RunAsync(args As String()) As Task
        Dim builder = Host.CreateApplicationBuilder(args)
        builder.Logging.AddConsole(Sub(o) o.LogToStandardErrorThreshold = LogLevel.Trace)
        builder.Services _
            .AddMcpServer() _
            .WithStdioServerTransport() _
            .WithToolsFromAssembly()
        Await builder.Build().RunAsync()
    End Function
End Module
