using System.Reflection.Metadata.Ecma335;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Task.Connector.Constants;
using Task.Connector.Data;
using Task.Connector.Exceptions;
using Task.Connector.Extensions;
using Task.Integration.Data.DbCommon;
using Task.Integration.Data.DbCommon.DbModels;
using Task.Integration.Data.Models;
using Task.Integration.Data.Models.Models;

namespace Task.Connector
{
    // Я бы реализовал еще один интерфейс: IAsyncConnector
    public class ConnectorDb : IConnector
    {
        private ApplicationContextFactory _contextFactory = null!;

        /* Логгер стоит инициализировать в конструкторе, а не извне.*/
        public ILogger Logger { get; set; } = null!;

        /*
         * Проблема получения Connection String в StartUp, возможно стоило бы получать абстракцию для работы с БД в конструкторе:
         * IRepository или IDataContext(Если ConnectorDb, будет зарегистрирован в DI как Scoped) или IDbContextFactory<DataContext>
         * Но надо понимать суть ConnectorDb, в данном случае я реализовывал его, как сущность, которая занимается бизнес логикой
         */

        public void StartUp(string connectionString)
        {
            _contextFactory = new ApplicationContextFactory(connectionString);
        }

        public void CreateUser(UserToCreate user)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));

            var traceId = Guid.NewGuid();
            using var context = _contextFactory.EnsureCreate();
            Logger.Debug($"TraceId: {traceId}. Adding a new user {JsonConvert.SerializeObject(user)}");

            if (IsUserExists(user.Login))
                throw new UserAlreadyRegisteredException($"TraceId: {traceId}. A user with this login already exists");

            var transaction = context.Database.BeginTransaction();

            var properties = user.Properties.ToList();
            try
            {
                bool.TryParse(
                    user.Properties.FirstOrDefault(x => x.Name.Equals(UserPropertiesDefaults.IS_LEAD_PROPERTY,
                        StringComparison.InvariantCultureIgnoreCase))?.Value,
                    out var isLead);

                var firstName = user.Properties.FirstOrDefault(x =>
                    x.Name.Equals(UserPropertiesDefaults.FIRST_NAME_PROPERTY,
                        StringComparison.InvariantCultureIgnoreCase));

                var middleName = user.Properties.FirstOrDefault(x =>
                    x.Name.Equals(UserPropertiesDefaults.MIDDLE_NAME_PROPERTY,
                        StringComparison.InvariantCultureIgnoreCase));

                var lastName = user.Properties.FirstOrDefault(x =>
                    x.Name.Equals(UserPropertiesDefaults.LAST_NAME_PROPERTY,
                        StringComparison.InvariantCultureIgnoreCase));

                var telephone =
                    user.Properties.FirstOrDefault(i => i.Name.Equals(UserPropertiesDefaults.TELEPHONE_NUMBER_PROPERTY,
                        StringComparison.InvariantCultureIgnoreCase));

                context.Users.Add(new User()
                {
                    Login = user.Login,
                    FirstName = firstName != null
                        ? firstName.Value
                        : string.Empty,
                    MiddleName = middleName != null
                        ? middleName.Value
                        : string.Empty,
                    LastName = lastName != null
                        ? lastName.Value
                        : string.Empty,
                    TelephoneNumber = telephone != null
                        ? telephone.Value
                        : string.Empty,
                    IsLead = properties.Any(i => i.Name == UserPropertiesDefaults.IS_LEAD_PROPERTY)
                        ? isLead
                        : Convert.ToBoolean("false")
                });

                context.Passwords.Add(new Sequrity()
                {
                    Password = user.HashPassword,
                    UserId = user.Login
                });

                context.SaveChanges();

                transaction.Commit();
            }
            catch (Exception e)
            {
                transaction.Rollback();

                Logger.Error(
                    $"TraceId: {traceId}. An exception occurred when adding a user {JsonConvert.SerializeObject(user)}. \n Exception: {e.Message}");
                throw new TransactionException(e);
            }
        }

        // Я бы возвращал IReadonlyList<Property> или Property[] 
        public IEnumerable<Property> GetAllProperties()
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));

            Logger.Debug($"TraceId: {Guid.NewGuid()}. Getting all properties for users");
            return typeof(User).GetProperties()
                .Select(i => new Property(i.Name, i.Name));
        }

        // Я бы возвращал IReadonlyList<UserProperty> или UserProperty[] 
        public IEnumerable<UserProperty> GetUserProperties(string userLogin)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));

            var traceId = Guid.NewGuid();
            using var context = _contextFactory.EnsureCreate();

            if (!IsUserExists(context, userLogin, out var user))
            {
                Logger.Warn($"TraceId: {traceId}. User with login: {userLogin} is not registered");
                throw new UserNotRegisteredException(
                    $"Unfortunately, no user with this username was found {userLogin}");
            }


            return typeof(User).GetProperties()
                .Where(p => p.Name != nameof(User.Login))
                .Select(p => new UserProperty(p.Name, p.GetValue(user)?.ToString() ?? string.Empty));
        }

        public bool IsUserExists(string userLogin)
        {
            using var context = _contextFactory.EnsureCreate();
            return IsUserExists(context, userLogin, out _);
        }

        private static bool IsUserExists(DataContext context, string login, out User? user)
        {
            user = context.Users.FirstOrDefault(x => x.Login == login);
            return user != null;
        }

        // Я бы принимал IReadonlyList<UserProperty> или UserProperty[] 
        public void UpdateUserProperties(IEnumerable<UserProperty> properties, string userLogin)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));

            var propertiesList = properties.ToList();
            if (properties == null || !propertiesList.Any())
                return;
            
            var traceId = Guid.NewGuid();

            Logger.Debug(
                $"TraceId: {traceId}. Updates to user properties. Login - {userLogin}, Properties - {JsonConvert.SerializeObject(propertiesList)}");

            using var context = _contextFactory.EnsureCreate();
            if (!IsUserExists(context, userLogin, out _))
            {
                Logger.Warn($"TraceId: {traceId}. User with login: {userLogin} is not registered");
                throw new UserNotRegisteredException(
                    $"Unfortunately, no user with this username was found {userLogin}");
            }

            try
            {
                context.Users.Where(x => x.Login == userLogin)
                    .ExecutePatchUpdate(b =>
                    {
                        var firstName =
                            propertiesList.FirstOrDefault(i => i.Name.Equals(UserPropertiesDefaults.FIRST_NAME_PROPERTY,
                                StringComparison.InvariantCultureIgnoreCase));
                        if (firstName != null)
                            b.SetProperty(p => p.FirstName, firstName.Value);

                        var middleName =
                            propertiesList.FirstOrDefault(i =>
                                i.Name.Equals(UserPropertiesDefaults.MIDDLE_NAME_PROPERTY,
                                    StringComparison.InvariantCultureIgnoreCase));
                        if (middleName != null)
                            b.SetProperty(p => p.MiddleName, middleName.Value);

                        var lastName =
                            propertiesList.FirstOrDefault(i => i.Name.Equals(UserPropertiesDefaults.LAST_NAME_PROPERTY,
                                StringComparison.InvariantCultureIgnoreCase));
                        if (lastName != null)
                            b.SetProperty(p => p.LastName, lastName.Value);

                        var telephoneNumber =
                            propertiesList.FirstOrDefault(i =>
                                i.Name.Equals(UserPropertiesDefaults.TELEPHONE_NUMBER_PROPERTY,
                                    StringComparison.InvariantCultureIgnoreCase));
                        if (telephoneNumber != null)
                            b.SetProperty(p => p.TelephoneNumber, telephoneNumber.Value);

                        var isLead =
                            propertiesList.FirstOrDefault(i => i.Name.Equals(UserPropertiesDefaults.IS_LEAD_PROPERTY,
                                StringComparison.InvariantCultureIgnoreCase));

                        if (isLead != null && bool.TryParse(isLead.Value, out var isLeadValue))
                            b.SetProperty(p => p.IsLead, isLeadValue);
                    });
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"TraceId: {traceId}. An exception occurred when updating user properties. Exception: {e.Message}");

                throw new UpdateRecordException(e);
            }
        }

        // Я бы возвращал IReadonlyList<Permission> или Permission[]
        public IEnumerable<Permission> GetAllPermissions()
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));
            
            var traceId = Guid.NewGuid();
            Logger.Debug($"TraceId: {traceId}. Getting all available rights");

            using var context = _contextFactory.EnsureCreate();
            var itRoles = context.ITRoles.Select(i => new Permission(i.Id.ToString()!, i.Name, "Все роли"))
                .ToList();

            var requestRights = context.RequestRights
                .Select(i => new Permission(i.Id.ToString()!, i.Name, "Все доступные права"))
                .ToList();

            itRoles.AddRange(requestRights);
            Logger.Debug($"TraceId: {traceId}. Result: {JsonConvert.SerializeObject(itRoles)}");

            return itRoles;
        }

        // Я бы принимал IReadonlyList<string> или string[] 
        public void AddUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));

            var rightIdsList = rightIds.ToList();
            if (rightIds == null || !rightIdsList.Any())
                return;

            var traceId = Guid.NewGuid();

            Logger.Debug($"TraceId: {traceId}. Adding user Permissions. Login: {userLogin}");
            using var context = _contextFactory.EnsureCreate();

            if (!IsUserExists(context, userLogin, out _))
            {
                Logger.Warn($"TraceId: {traceId}. User with login: {userLogin} is not registered");
                throw new UserNotRegisteredException(
                    $"Unfortunately, no user with this username was found {userLogin}");
            }

            var rolesList = new List<UserITRole>();
            var userRequestRightsList = new List<UserRequestRight>();

            foreach (var rightId in rightIdsList)
            {
                var roleValues = rightId.Split(':', 2);
                var roleValue = int.Parse(roleValues[1]);

                switch (roleValues[0])
                {
                    case RolesConstants.ROLE:
                        rolesList.Add(new UserITRole()
                        {
                            UserId = userLogin,
                            RoleId = roleValue
                        });

                        break;

                    case RolesConstants.REQUEST:
                        userRequestRightsList.Add(new UserRequestRight()
                        {
                            UserId = userLogin,
                            RightId = roleValue
                        });
                        break;
                }
            }

            using var transaction = context.Database.BeginTransaction();

            try
            {
                context.BulkInsert(rolesList);
                context.BulkInsert(userRequestRightsList);

                transaction.Commit();
            }
            catch (Exception e)
            {
                transaction.Rollback();
                Logger.Error(
                    $"TraceId: {traceId}. Exception when adding Permission for a user {userLogin}. Roles {JsonConvert.SerializeObject(rolesList)}, " +
                    $"Rights: {JsonConvert.SerializeObject(userRequestRightsList)}. Exception: {e.Message}");

                throw new TransactionException(e);
            }
        }

        // Я бы принимал IReadonlyList<string> или string[] 
        public void RemoveUserPermissions(string userLogin, IEnumerable<string> rightIds)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));
            
            var rightIdsList = rightIds.ToList();
            if (rightIds == null || !rightIdsList.Any())
                return;

            var traceId = Guid.NewGuid();
            Logger.Debug($"TraceId: {traceId}. Removing user Permissions. Login: {userLogin}");

            if (!IsUserExists(userLogin))
            {
                Logger.Warn($"TraceId: {traceId}. User with login: {userLogin} is not registered");
                throw new UserNotRegisteredException(
                    $"Unfortunately, no user with this username was found {userLogin}");
            }

            using var context = _contextFactory.EnsureCreate();
            var rIds = rightIdsList.Select(x => x.Split(":")[1]).ToList();
            if (rIds.Count <= 0)
            {
                Logger.Warn(
                    $"TraceId: {traceId}. Rights Ids After Parse Equals Zero. Source: {JsonConvert.SerializeObject(rightIds)}");
            }

            context.UserRequestRights.Where(x => x.UserId == userLogin && rIds.Contains(x.RightId.ToString()))
                .ExecuteDelete();
        }

        // Я бы возвращал IReadonlyList<string> или string[] 
        public IEnumerable<string> GetUserPermissions(string userLogin)
        {
            ArgumentNullException.ThrowIfNull(Logger, nameof(Logger));
            
            var traceId = Guid.NewGuid();

            Logger.Debug($"TraceId: {traceId}. Getting User Permissions. Login: {userLogin}");
            if (!IsUserExists(userLogin))
            {
                Logger.Warn($"TraceId: {traceId}. User with login: {userLogin} is not registered");
                throw new UserNotRegisteredException(
                    $"Unfortunately, no user with this username was found {userLogin}");
            }

            using var context = _contextFactory.EnsureCreate();

            var userRequestRights = context.UserRequestRights
                .Where(x => x.UserId == userLogin)
                .Select(x => x.RightId)
                .ToList();

            var result = context.RequestRights
                .Where(x => userRequestRights.Contains((int)x.Id))
                .Select(x => x.Name)
                .ToList();

            Logger.Debug($"TraceId: {traceId}. Result {JsonConvert.SerializeObject(result)}");

            return result;
        }
    }
}