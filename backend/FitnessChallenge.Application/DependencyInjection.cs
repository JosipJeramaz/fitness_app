using FitnessChallenge.Application.Services;
using FitnessChallenge.Domain.Scoring;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessChallenge.Application;

/// <summary>Composition root for the application layer — keeps <c>Program.cs</c> clean.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoring is stateless & pure → safe as a singleton.
        services.AddSingleton<IScoringService, ScoringService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
