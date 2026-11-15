using ProjectDoomsdayServer.Domain.DbModels;

namespace ProjectDoomsdayServer.Application.Interfaces;

public interface IUserService
{
    public User GetUser(string id);

    // wip, ignore for now

    public void DeleteUser(string id);
    public void CreateUser();
    public void UpdateUser();
}
