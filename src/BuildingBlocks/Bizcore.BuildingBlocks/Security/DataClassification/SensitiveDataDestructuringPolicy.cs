using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Reflection;

namespace Bizcore.BuildingBlocks.Security.DataClassification
{
    /// <summary>
    /// Chính sách Serilog Destructuring để tự động mask các thuộc tính được đánh dấu [SensitiveData].
    /// </summary>
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cache = new();

        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
        {
            var type = value.GetType();

            // Chỉ xử lý các class nghiệp vụ của Bizcore (bỏ qua primitive types, collections, system types)
            if (type.IsPrimitive || type.IsEnum || type.Namespace?.StartsWith("System") == true)
            {
                result = null!;
                return false;
            }

            var properties = _cache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

            var logEventProperties = new List<LogEventProperty>();
            bool hasSensitiveData = false;

            foreach (var prop in properties)
            {
                object? propValue = null;
                try { propValue = prop.GetValue(value); } catch { continue; }

                var sensitiveAttr = prop.GetCustomAttribute<SensitiveDataAttribute>();
                if (sensitiveAttr != null)
                {
                    if (sensitiveAttr.Level == ClassificationLevel.Restricted)
                    {
                        // Không bao giờ ghi log cho dữ liệu Restricted (ví dụ Password)
                        hasSensitiveData = true;
                        continue; 
                    }

                    hasSensitiveData = true;
                    logEventProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(sensitiveAttr.Mask)));
                }
                else
                {
                    logEventProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propValue, true)));
                }
            }

            if (hasSensitiveData)
            {
                result = new StructureValue(logEventProperties, type.Name);
                return true;
            }

            result = null!;
            return false;
        }
    }
}
