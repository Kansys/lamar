using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JasperFx.Core;
using JasperFx.Core.Reflection;
using JasperFx.Core.TypeScanning;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable 1591

[assembly: IgnoreAssembly]

namespace Lamar.Scanning.Conventions;

[LamarIgnore]
public class AssemblyScanner : IAssemblyScanner
{
    private readonly List<Assembly> _assemblies = new();
    private readonly CompositeFilter<Type> _filter = new();
    private readonly ServiceRegistry _parent;
    private bool _hasScanned;

    public AssemblyScanner(ServiceRegistry parent)
    {
        _parent = parent;

        Exclude(type => type.HasAttribute<LamarIgnoreAttribute>());
    }

    public List<IRegistrationConvention> Conventions { get; } = new();

    public TypeSet TypeFinder { get; private set; }


    public string Description { get; set; }


    public void Assembly(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }
    }

    public void Assembly(string assemblyName)
    {
        Assembly(AssemblyLoader.ByName(assemblyName));
    }

    public void Convention<T>() where T : IRegistrationConvention, new()
    {
        var previous = Conventions.FirstOrDefault(scanner => scanner is T);
        if (previous == null)
        {
            With(new T());
        }
    }

    public void AssemblyContainingType<T>()
    {
        AssemblyContainingType(typeof(T));
    }

    public void AssemblyContainingType(Type type)
    {
        _assemblies.Add(type.GetTypeInfo().Assembly);
    }

    public FindAllTypesFilter AddAllTypesOf<TServiceType>()
    {
        return AddAllTypesOf(typeof(TServiceType), ServiceLifetime.Transient);
    }

    public FindAllTypesFilter AddAllTypesOf<TServiceType>(ServiceLifetime lifetime)
    {
        return AddAllTypesOf(typeof(TServiceType), lifetime);
    }

    public FindAllTypesFilter AddAllTypesOf(Type pluginType)
    {
        return AddAllTypesOf(pluginType, ServiceLifetime.Transient);
    }

    public FindAllTypesFilter AddAllTypesOf(Type pluginType, ServiceLifetime lifetime)
    {
        var filter = new FindAllTypesFilter(pluginType, lifetime);
        With(filter);

        return filter;
    }


    public void Exclude(Func<Type, bool> exclude)
    {
        _filter.Excludes += exclude;
    }

    public void ExcludeNamespace(string nameSpace)
    {
        Exclude(type => type.IsInNamespace(nameSpace));
    }

    public void ExcludeNamespaceContainingType<T>()
    {
        ExcludeNamespace(typeof(T).Namespace);
    }

    public void Include(Func<Type, bool> predicate)
    {
        _filter.Includes += predicate;
    }

    public void IncludeNamespace(string nameSpace)
    {
        Include(type => type.IsInNamespace(nameSpace));
    }

    public void IncludeNamespaceContainingType<T>()
    {
        IncludeNamespace(typeof(T).Namespace);
    }

    public void ExcludeType<T>()
    {
        Exclude(type => type == typeof(T));
    }

    public void With(IRegistrationConvention convention)
    {
        Conventions.Fill(convention);
    }

    public void WithDefaultConventions()
    {
        WithDefaultConventions(ServiceLifetime.Transient);
    }

    public void WithDefaultConventions(ServiceLifetime lifetime)
    {
        var convention = new DefaultConventionScanner(lifetime);
        With(convention);
    }

    public void WithDefaultConventions(OverwriteBehavior behavior)
    {
        WithDefaultConventions(behavior, ServiceLifetime.Transient);
    }

    public void WithDefaultConventions(OverwriteBehavior behavior, ServiceLifetime lifetime)
    {
        var convention = new DefaultConventionScanner(lifetime)
        {
            Overwrites = behavior
        };

        With(convention);
    }

    public void ConnectImplementationsToTypesClosing(Type openGenericType)
    {
        ConnectImplementationsToTypesClosing(openGenericType, ServiceLifetime.Transient);
    }

    public void ConnectImplementationsToTypesClosing(Type openGenericType, ServiceLifetime lifetime)
    {
        var convention = new GenericConnectionScanner(openGenericType, lifetime);
        With(convention);
    }

    public void RegisterConcreteTypesAgainstTheFirstInterface()
    {
        RegisterConcreteTypesAgainstTheFirstInterface(ServiceLifetime.Transient);
    }

    public void RegisterConcreteTypesAgainstTheFirstInterface(ServiceLifetime lifetime)
    {
        var convention = new FirstInterfaceConvention(lifetime);
        With(convention);
    }

    public void SingleImplementationsOfInterface()
    {
        SingleImplementationsOfInterface(ServiceLifetime.Transient);
    }

    public void SingleImplementationsOfInterface(ServiceLifetime lifetime)
    {
        var convention = new ImplementationMap(lifetime);
        With(convention);
    }

    public void LookForRegistries()
    {
        Convention<FindRegistriesScanner>();
    }


    public void TheCallingAssembly()
    {
        if (_parent.GetType().Assembly != typeof(ServiceRegistry).Assembly)
        {
            Assembly(_parent.GetType().Assembly);
            return;
        }

        var callingAssembly = CallingAssembly.Find();

        if (callingAssembly != null)
        {
            Assembly(callingAssembly);
        }
        else
        {
            throw new InvalidOperationException(
                "Could not determine the calling assembly, you may need to explicitly call IAssemblyScanner.Assembly()");
        }
    }

    public void AssembliesFromApplicationBaseDirectory()
    {
        AssembliesFromApplicationBaseDirectory(a => true);
    }

    public void AssembliesFromApplicationBaseDirectory(Func<Assembly, bool> assemblyFilter)
    {
        var assemblies = AssemblyFinder.FindAssemblies(assemblyFilter);

        foreach (var assembly in assemblies) Assembly(assembly);
    }

    /// <summary>
    ///     Choosing option will direct Jasper to *also* scan files ending in '*.exe'
    /// </summary>
    /// <param name="scanner"></param>
    /// <param name="assemblyFilter"></param>
    /// <param name="includeExeFiles"></param>
    public void AssembliesAndExecutablesFromApplicationBaseDirectory(Func<Assembly, bool> assemblyFilter = null)
    {
        var assemblies = AssemblyFinder.FindAssemblies(assemblyFilter, true);

        foreach (var assembly in assemblies) Assembly(assembly);
    }

    [Obsolete("It is very strongly recommended to use the overload with an Assembly filter to improve performance")]
    public void AssembliesAndExecutablesFromPath(string path)
    {
        var assemblies = AssemblyFinder.FindAssemblies(a => true, path,
            txt => { Console.WriteLine("Lamar could not load assembly from file " + txt); }, true);

        foreach (var assembly in assemblies) Assembly(assembly);
    }

    [Obsolete("It is very strongly recommended to use the overload with an Assembly filter to improve performance")]
    public void AssembliesFromPath(string path)
    {
        var assemblies = AssemblyFinder.FindAssemblies(a => true, path,
            txt => { Console.WriteLine("Lamar could not load assembly from file " + txt); }, false);

        foreach (var assembly in assemblies) Assembly(assembly);
    }

    public void AssembliesAndExecutablesFromPath(string path,
        Func<Assembly, bool> assemblyFilter)
    {
        var assemblies = AssemblyFinder.FindAssemblies(assemblyFilter, path,
            txt => { Console.WriteLine("Lamar could not load assembly from file " + txt); }, true);


        foreach (var assembly in assemblies) Assembly(assembly);
    }

    public void AssembliesFromPath(string path,
        Func<Assembly, bool> assemblyFilter)
    {
        var assemblies = AssemblyFinder.FindAssemblies(assemblyFilter, path,
            txt => { Console.WriteLine("Lamar could not load assembly from file " + txt); }, false);


        foreach (var assembly in assemblies) Assembly(assembly);
    }

    public void Describe(StringWriter writer)
    {
        writer.WriteLine(Description);
        writer.WriteLine("Assemblies");
        writer.WriteLine("----------");

        _assemblies.OrderBy(x => x.FullName).Each(x => writer.WriteLine("* " + x));
        writer.WriteLine();

        writer.WriteLine("Conventions");
        writer.WriteLine("--------");
        Conventions.Each(x => writer.WriteLine("* " + x));
    }

    public void Start()
    {
        if (_hasScanned) return;
        _hasScanned = true;
        
        if (!Conventions.Any())
        {
            throw new InvalidOperationException(
                $"There are no {nameof(IRegistrationConvention)}'s in this scanning operation. ");
        }

        TypeFinder = TypeRepository.FindTypes(_assemblies, type => _filter.Matches(type));
    }

    public void ApplyRegistrations(ServiceRegistry services)
    {
        foreach (var convention in Conventions) convention.ScanTypes(TypeFinder, services);
    }

    public bool Contains(string assemblyName)
    {
        return _assemblies
            .Select(assembly => new AssemblyName(assembly.FullName))
            .Any(aName => aName.Name == assemblyName);
    }
}

#pragma warning restore 1591