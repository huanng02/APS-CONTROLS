using System.Collections.Generic;

namespace QuanLyGiuXe.Services
{
    public interface IGenericService<T>
    {
        List<T> GetAll();
        int Insert(T entity);
        void Update(T entity);
        void Delete(int id);
    }
}
