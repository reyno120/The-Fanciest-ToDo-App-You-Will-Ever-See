﻿using SharedKernel;

namespace FancyToDo.Core.ToDoList.DomainEvents;

public record ItemAddedEvent(Guid ToDoListId, ToDoItem Item): BaseDomainEvent;