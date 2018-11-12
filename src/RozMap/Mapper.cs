using System;

namespace RozMap
{
    public class Mapper
    {
        private readonly MapperConfiguration _configuration;

        public Mapper(MapperConfiguration configuration)
        {
            _configuration = configuration;
        }

        public TDest Map<TSource, TDest>(TSource source)
        {
            return (TDest)Map(typeof(TSource), typeof(TDest), source);
        }

        public object Map(Type sourceType, Type destType, object source)
        {
            var mapper = _configuration.GetMapperFor(sourceType, destType);
            var destInstance = mapper.MapInstance(source);
            return destInstance;
        }
    }

    public interface IPerformInstanceMapping
    {
        object MapInstance(object sourceInstance);
    }
}