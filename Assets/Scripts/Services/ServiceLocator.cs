using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, Type> _services = new Dictionary<Type, Type>();
    private static readonly Dictionary<Type, object> _serviceInstances = new Dictionary<Type, object>();

    static ServiceLocator()
    {
        AutoRegisterServices();
    }

    private static void AutoRegisterServices()
    {
        var serviceInterfaceType = typeof(IService);
        var assembly = Assembly.GetExecutingAssembly();

        var serviceTypes = assembly.GetTypes()
            .Where(t => serviceInterfaceType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

        foreach (var serviceType in serviceTypes)
        {
            _services[serviceType] = serviceType;
        }
    }

    public static T GetService<T>() where T : class, IService
    {
        var serviceType = typeof(T);
        if (_serviceInstances.TryGetValue(serviceType, out var serviceInstance))
        {
            return (T)serviceInstance;
        }

        if (!_services.TryGetValue(serviceType, out var service))
            throw new Exception($"Service of type {serviceType.Name} is not registered.");
        
        var instance = (T)Activator.CreateInstance(service);
        _serviceInstances[serviceType] = instance;
        return instance;

    }
}