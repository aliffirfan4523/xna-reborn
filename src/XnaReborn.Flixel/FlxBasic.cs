using XnaReborn.As3;

namespace org.flixel;

/// <summary>Base of everything that can be updated and drawn. Written for XNA Reborn against the public Flixel 2.x API.</summary>
public class FlxBasic
{
    /// <summary>A free number for the game to use.</summary>
    public int ID = -1;

    /// <summary>False means the object is dead or recycled: it is not updated, drawn or collided.</summary>
    public bool exists = true;

    /// <summary>False means the object is not updated.</summary>
    public bool active = true;

    /// <summary>False means the object is not drawn.</summary>
    public bool visible = true;

    /// <summary>Whether the object is alive. Kill sets it false.</summary>
    public bool alive = true;

    /// <summary>Releases resources. Does nothing by default.</summary>
    public virtual void destroy()
    {
    }

    /// <summary>Runs before <see cref="update"/>.</summary>
    public virtual void preUpdate()
    {
    }

    /// <summary>Runs once per frame.</summary>
    public virtual void update()
    {
    }

    /// <summary>Runs after <see cref="update"/>.</summary>
    public virtual void postUpdate()
    {
    }

    /// <summary>Draws the object.</summary>
    public virtual void draw()
    {
    }

    /// <summary>Marks the object dead and removes it from play.</summary>
    public virtual void kill()
    {
        alive = false;
        exists = false;
    }

    /// <summary>Brings the object back to life.</summary>
    public virtual void revive()
    {
        alive = true;
        exists = true;
    }

    /// <inheritdoc />
    public override string ToString() => GetType().Name;
}

/// <summary>A collection of <see cref="FlxBasic"/> objects that is updated, drawn and collided as one.</summary>
public class FlxGroup : FlxBasic
{
    /// <summary>The members. A slot is null after a removal.</summary>
    public List<FlxBasic?> members = new();

    /// <summary>Number of slots in <see cref="members"/>.</summary>
    public int length => members.Count;

    /// <summary>Adds an object (once) and returns it.</summary>
    public FlxBasic add(FlxBasic Object)
    {
        if (!members.Contains(Object))
        {
            var free = members.IndexOf(null);
            if (free >= 0)
            {
                members[free] = Object;
            }
            else
            {
                members.Add(Object);
            }
        }

        return Object;
    }

    /// <summary>Removes an object. With <paramref name="Splice"/> the slot is deleted, otherwise it becomes null.</summary>
    public FlxBasic? remove(FlxBasic Object, bool Splice = false)
    {
        var i = members.IndexOf(Object);
        if (i < 0)
        {
            return null;
        }

        if (Splice)
        {
            members.RemoveAt(i);
        }
        else
        {
            members[i] = null;
        }

        return Object;
    }

    /// <summary>
    /// Returns the first member that does not exist (and revives it). When there is none and <paramref name="ObjectClass"/> is
    /// given, makes a new one and adds it.
    /// </summary>
    public FlxBasic? recycle(AsClass? ObjectClass = null)
    {
        var free = getFirstAvailable(ObjectClass);
        if (free is not null)
        {
            free.revive();
            return free;
        }

        if (ObjectClass?.Type is null)
        {
            return null;
        }

        return add((FlxBasic)ObjectClass.New());
    }

    /// <summary>Returns the first member that does not exist, optionally of the given class.</summary>
    public FlxBasic? getFirstAvailable(AsClass? ObjectClass = null) =>
        members.FirstOrDefault(m => m is not null && !m.exists && (ObjectClass?.Type is null || ObjectClass.Type.IsInstanceOfType(m)));

    /// <summary>Returns the first member that is alive.</summary>
    public FlxBasic? getFirstAlive() => members.FirstOrDefault(m => m is not null && m.exists && m.alive);

    /// <summary>Returns the first member that is dead.</summary>
    public FlxBasic? getFirstDead() => members.FirstOrDefault(m => m is not null && !m.alive);

    /// <summary>Number of members that exist and are alive.</summary>
    public int countLiving() => members.Count(m => m is not null && m.exists && m.alive);

    /// <summary>Number of members that are dead.</summary>
    public int countDead() => members.Count(m => m is not null && !m.alive);

    /// <summary>Calls <paramref name="Function"/> on every non-null member that exists.</summary>
    public void callAll(Action<FlxBasic> Function)
    {
        foreach (var m in members.ToList())
        {
            if (m is not null)
            {
                Function(m);
            }
        }
    }

    /// <summary>Updates members in order: pre-update, update, post-update, for each in turn.</summary>
    public override void update()
    {
        foreach (var m in members.ToList())
        {
            if (m is { exists: true, active: true })
            {
                m.preUpdate();
                m.update();
                m.postUpdate();
            }
        }
    }

    /// <inheritdoc />
    public override void postUpdate()
    {
    }

    /// <inheritdoc />
    public override void draw()
    {
        foreach (var m in members.ToList())
        {
            if (m is { exists: true, visible: true })
            {
                m.draw();
            }
        }
    }

    /// <inheritdoc />
    public override void kill()
    {
        foreach (var m in members.ToList())
        {
            if (m is { exists: true })
            {
                m.kill();
            }
        }

        base.kill();
    }

    /// <inheritdoc />
    public override void destroy()
    {
        foreach (var m in members)
        {
            m?.destroy();
        }

        members.Clear();
    }
}

/// <summary>One screen of the game. Override <see cref="create"/> to add objects.</summary>
public class FlxState : FlxGroup
{
    /// <summary>Called once when the state starts.</summary>
    public virtual void create()
    {
    }
}
