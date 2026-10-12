global using System.Net;
global using System.Net.Http.Json;
global using System.Text;
global using System.Text.Json;
global using FluentAssertions;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.Extensions.DependencyInjection;
global using Evolith.Ums.Application.Common.Interfaces;
global using Evolith.Ums.Domain.Authorization;
global using Evolith.Ums.Domain.Configuration;
global using Evolith.Ums.Domain.Configuration.AppConfiguration;
global using Evolith.Ums.Domain.Configuration.FeatureFlag;
global using Evolith.Ums.Domain.Configuration.IdpConfiguration;
global using Evolith.Ums.Domain.Enums;
global using Evolith.Ums.Domain.Identity;
global using Evolith.Ums.Domain.Kernel.ValueObjects;
global using Evolith.Ums.Infrastructure.Persistence;
global using Evolith.Ums.Infrastructure.Persistence.Authorization;
global using Evolith.Ums.Infrastructure.Persistence.Configuration;
global using Evolith.Ums.Infrastructure.Persistence.Identity;
global using BeyondNetCode.Shell.Aop.Aspects;
global using Evolith.Ums.Infrastructure.Observability;
global using BeyondNetCode.Shell.Ddd;
global using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
