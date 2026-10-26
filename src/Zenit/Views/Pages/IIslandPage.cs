using Zenit.Services;

namespace Zenit.Views.Pages;

/// <summary>Página del carrusel de la isla expandida.</summary>
public interface IIslandPage
{
    PageKind Kind { get; }

    /// <summary>Actualiza los datos visibles; se llama cada segundo mientras la isla está expandida.</summary>
    void Refresh();

    /// <summary>Animación continua (opcional), en cada frame mientras la página está a la vista.</summary>
    void Animate(double time) { }
}
