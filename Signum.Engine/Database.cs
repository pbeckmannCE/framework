﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Utilities;
using Signum.Engine;
using System.Linq.Expressions;
using System.Reflection;
using Signum.Engine.Linq;
using Signum.Engine.Maps;
using Signum.Utilities.ExpressionTrees;
using Signum.Entities.Reflection;
using Signum.Engine.Exceptions;

namespace Signum.Engine
{
    public static class Database
    {
        #region Save
        public static void SaveList<T>(this IEnumerable<T> entities)
            where T : IIdentifiable
        {
            SaveParams(entities.Cast<IdentifiableEntity>().ToArray());
        }

        public static void SaveParams(params IdentifiableEntity[] entities)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Saver.SaveAll(entities);

                tr.Commit();
            }
        }

        public static T Save<T>(this T obj)
            where T : IdentifiableEntity
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Saver.Save(obj);

                return tr.Commit(obj);
            }
        }
        #endregion

        #region Retrieve
        public static T Retrieve<T>(this Lazy<T> lazy) where T : class, IIdentifiable
        {
            return lazy.EntityOrNull ?? (lazy.EntityOrNull = (T)(object)Retrieve(lazy.RuntimeType, lazy.Id));
        }

        public static T RetrieveAndForget<T>(this Lazy<T> lazy) where T : class, IIdentifiable
        {
            return (T)(object)Retrieve(lazy.RuntimeType, lazy.Id);
        }

        public static IdentifiableEntity Retrieve(Lazy lazy)
        {
            return lazy.UntypedEntityOrNull ?? (lazy.UntypedEntityOrNull = Retrieve(lazy.RuntimeType, lazy.Id));
        }

        public static IdentifiableEntity RetrieveAndForget(Lazy lazy)
        {
            return Retrieve(lazy.RuntimeType, lazy.Id);
        }

        public static T Retrieve<T>(int id) where T : IdentifiableEntity
        {
            return (T)Retrieve(typeof(T), id);
        }

        public static IdentifiableEntity Retrieve(Type type, int id)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                IdentifiableEntity ident = rec.Retrieve(type, id);

                rec.ProcessAll();

                return tr.Commit(ident);
            }
        }
        #endregion

        #region Exists
        public static bool Exists<T>(int id)
            where T : IdentifiableEntity
        {
            return Exists(typeof(T), id);
        }

        public static bool Exists(Type type, int id)
        {
            using (Transaction tr = new Transaction())
            {
                Table t = Schema.Current.Table(type);

                SqlPreCommand command = SqlBuilder.SelectCount(t.Name, id);

                int? count = (int?)Executor.ExecuteScalar(command.ToSimple());

                return tr.Commit(count == 1);
            }
        }
        #endregion

        #region Retrieve All Lists Lazys
        public static List<T> RetrieveAll<T>()
        {
            return RetrieveAll(typeof(T)).Cast<T>().ToList();
        }

        public static List<IdentifiableEntity> RetrieveAll(Type type)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<IdentifiableEntity> ident = rec.RetrieveAll(type);

                rec.ProcessAll(); 

                return tr.Commit(ident);
            }
        }

        public static List<Lazy<T>> RetrieveAllLazy<T>()
            where T : class, IIdentifiable
        {
            return RetrieveAllLazy(typeof(T)).Cast<Lazy<T>>().ToList();
        }

        public static List<Lazy> RetrieveAllLazy(Type type)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<Lazy> ident = rec.RetrieveAllLazy(type);

                rec.ProcessAll(); 

                return tr.Commit(ident);
            }
        }

        public static List<T> RetrieveList<T>(List<int> ids)
        {
            return RetrieveList(typeof(T), ids).Cast<T>().ToList();
        }

        public static List<IdentifiableEntity> RetrieveList(Type type, List<int> ids)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<IdentifiableEntity> ident = rec.RetrieveList(type, ids);

                rec.ProcessAll();

                return tr.Commit(ident);
            }
        }

        public static List<Lazy<T>> RetrieveListLazy<T>(List<int> ids)
            where T : class, IIdentifiable
        {
            return RetrieveListLazy(typeof(T), ids).Cast<Lazy<T>>().ToList();
        }

        public static List<Lazy> RetrieveListLazy(Type type, List<int> ids)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<Lazy> ident = rec.RetrieveListLazy(type, ids);

                rec.ProcessAll();

                return tr.Commit(ident);
            }
        }

        public static List<T> RetrieveFromListOfLazy<T>(List<Lazy<T>> lazys)
         where T : class, IIdentifiable
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<T> ident = lazys.Select(l => (T)(IIdentifiable)rec.Retrieve(l)).ToList();

                rec.ProcessAll();

                return tr.Commit(ident);
            }
        }

        public static List<IdentifiableEntity> RetrieveFromListOfLazy(List<Lazy> lazys)
        {
            using (new EntityCache())
            using (Transaction tr = new Transaction())
            {
                Retriever rec = new Retriever();

                List<IdentifiableEntity> ident = lazys.Select(l => rec.Retrieve(l)).ToList();

                rec.ProcessAll(); 

                return tr.Commit(ident);
            }
        }

        #endregion

        #region Delete
        public static void Delete(Type type, int id)
        {
            using (Transaction tr = new Transaction())
            {
                Deleter.Delete(type, id);

                tr.Commit();
            }
        }

        public static void Delete(Type type, IEnumerable<int> ids)
        {
            using (Transaction tr = new Transaction())
            {
                Deleter.Delete(type, ids.ToList());

                tr.Commit();
            }
        }

        public static void Delete<T>(this T ident)
            where T : IdentifiableEntity
        {
            Delete(ident.GetType(), ident.Id);
        }

        public static void Delete<T>(IEnumerable<T> collection)
            where T : IdentifiableEntity
        {
            Delete(collection.Select(a => a.GetType()).Single(), collection.Select(i => i.Id));
        }
        #endregion

        #region Query Back
        public static IQueryable<T> Query<T>()
         where T : IdentifiableEntity
        {
            return new Query<T>(DbQueryProvider.Single);
        }

        public static IQueryable<T> View<T>()
            where T : IView
        {
            return new Query<T>(DbQueryProvider.Single);
        }
        #endregion

        public static int MaxParameters { get { return SqlBuilder.MaxParametersInSQL; } }

        public static int UnsafeDelete<T>(Expression<Func<T, bool>> predicate)
              where T : IdentifiableEntity
        {
            return QueryUtils.Delete<T>(predicate);
        }

        public static int UnsafeUpdate<T>(Expression<Func<T, T>> update, Expression<Func<T, bool>> predicate)
            where T : IdentifiableEntity
        {
            return QueryUtils.Update<T>(update, predicate);
        }

        public static T Random<T>()
        where T : IdentifiableEntity
        {
            int count = Database.Query<T>().Count();
            if (count == null)
                throw new InvalidOperationException("There are no {0} in the database".Formato(typeof(T).NiceName())); 
            
            int retries = 4; 

            for (int i = 0; i < retries; i++)
			{
                int id = MyRandom.Current.Next(1,  count+ 1);
                
                try
                {
                    return Database.Retrieve<T>(id); 
                }
                catch(EntityNotFoundException)
                {}
			}

            throw new InvalidOperationException("Impossible to get a Random {0} after {1} retries".Formato(typeof(T).NiceName(), retries));
        }
    }
}
