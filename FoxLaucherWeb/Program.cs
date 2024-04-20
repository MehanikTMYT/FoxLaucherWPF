class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var hostAddress = configuration["FoxLauncherWeb_HostAddress"];

        if (string.IsNullOrEmpty(hostAddress))
        {
            Console.WriteLine("Ошибка. Значение HostAddress пустое");
            return;
        }

        builder.WebHost.UseUrls("http://*:5000");

        builder.Services.AddControllers();

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}