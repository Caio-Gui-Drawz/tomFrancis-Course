// Marca um componente como "comportamento de movimento" do inimigo (Straight, Chase, Path, Lob...).
// Serve pra ferramentas (ex.: a cena de GYM) acharem e desligarem TODOS os movimentos de um prefab
// sem precisar conhecer cada script. Todo movimento novo deve implementar esta interface.
public interface IMovementBehavior
{
}
