#region License

/* Copyright (C) 2006-2007 Cristian Libardo
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 */

#endregion

using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using N2.Definitions;
using N2.Edit;
using N2.Engine;
using N2.Edit.Workflow;
using System.Web;
using N2.Persistence;
using N2.Edit.Web;

namespace N2.Web.UI.WebControls
{
	/// <summary>A form that generates an edit interface for content items.</summary>
	public class ItemEditor : WebControl, INamingContainer, IItemEditor, IContentForm<CommandContext>, IPlaceHolderAccessor
	{
		#region Constructor

		public ItemEditor()
		{
			CssClass = "itemEditor";
			Engine = N2.Context.Current;
		}

		public ItemEditor(ContentItem item)
			: this()
		{
			CurrentItem = item;
		}

		#endregion

		#region Private Fields
		private ContentItem currentItem;
		private IDictionary<string, Control> placeholders = new Dictionary<string, Control>();
		#endregion

		#region Properties

		public virtual IEngine Engine { get; set; }

		/// <summary>The content adapter related to the current page item.</summary>
		protected virtual EditableAdapter EditAdapter
		{
			get { return Engine.Resolve<IContentAdapterProvider>().ResolveAdapter<EditableAdapter>(CurrentItem); }
		}

		/// <summary>Gets a dictionary of editor controls added this control.</summary>
		[Obsolete("Use Editors instead")]
		public IDictionary<string, Control> AddedEditors
		{ 
			get { return Editors.ToDictionary(e => e.PropertyName, e => e.Control); } 
		}

		/// <summary>Gets a dictionary of editor controls added this control.</summary>
		public ContainableContext[] Editors { get; protected set; }
		
		protected override HtmlTextWriterTag TagKey
		{
			get { return HtmlTextWriterTag.Div; }
		}

		/// <summary>The type of item to edit. ItemEditor will look at <see cref="N2.Details.AbstractEditableAttribute"/> attributes on this type to render input controls.</summary>
		public string Discriminator
		{
			get { return (string)ViewState["Discriminator"] ?? string.Empty; }
			set { ViewState["Discriminator"] = value; }
		}

		/// <summary>The definition which is the source of editors.</summary>
		public ItemDefinition Definition { get; set; }

		/// <summary>Gets the type of the edited item.</summary>
		/// <returns>The item's type.</returns>
		public Type CurrentItemType
		{
			get
			{
				var definition = GetDefinition();
				if (definition != null)
					return definition.ItemType;

				return null;
			}
		}

		/// <summary>Gets the parent item id that the edited item will be set to.</summary>
		public string ParentPath
		{
			get { return (string)(ViewState["ParentPath"] ?? string.Empty); }
			set { ViewState["ParentPath"] = value; }
		}

		/// <summary>Gets or sets the item to edit with this form.</summary>
		public ContentItem CurrentItem
		{
			get
			{
				if (currentItem == null && !string.IsNullOrEmpty(Discriminator))
				{
					ContentItem parentItem = Engine.Resolve<Navigator>().Navigate(HttpUtility.UrlDecode(ParentPath));
					currentItem = Engine.Resolve<ContentActivator>().CreateInstance(CurrentItemType, parentItem);
					currentItem.ZoneName = ZoneName;
					ApplyContentModifications(ContentState.New);
				}
				return currentItem;
			}
			set
			{
				currentItem = value;
				if (value != null)
				{
					Definition = Engine.Container.ResolveAll<ITemplateProvider>().GetDefinition(value);
					Discriminator = Definition.Discriminator;
					if (value.VersionOf != null && value.ID == 0)
						VersioningMode = ItemEditorVersioningMode.SaveOnly;
					EnsureChildControls();
					Engine.EditManager.UpdateEditors(Definition, value, Editors, Page.User);
				}
				else
				{
					Discriminator = null;
				}
			}
		}

		public void ApplyContentModifications(ContentState changingTo)
		{
			foreach (var d in GetDefinition().ContentModifiers.Where(cm => (cm.ChangingTo & changingTo) == changingTo))
				d.Modify(currentItem);
		}

		/// <summary>Gets or sets the zone name that the edited item will be set to.</summary>
		public string ZoneName
		{
			get { return (string)ViewState["ZoneName"]; }
			set { ViewState["ZoneName"] = value; }
		}

		/// <summary>Gets or sets wether a version should be saved before the item is updated.</summary>
		public ItemEditorVersioningMode VersioningMode
		{
			get { return (ItemEditorVersioningMode)(ViewState["SaveVersion"] ?? ItemEditorVersioningMode.VersionAndSave); }
			set { ViewState["SaveVersion"] = value; }
		}

		#endregion

		#region Methods

		protected override void CreateChildControls()
		{
			var definition = GetDefinition();

			if (definition != null)
			{
				Editors = EditAdapter.AddDefinedEditors(definition, CurrentItem, this, Page.User).ToArray();
				if (!Page.IsPostBack)
				{
					EditAdapter.LoadAddedEditors(definition, CurrentItem, Editors, Page.User);
				}
			}

			base.CreateChildControls();
		}

		public ItemDefinition GetDefinition()
		{
			return Definition ?? Engine.Definitions.GetDefinition(Discriminator) ?? Engine.Definitions.GetDefinition(CurrentItemType);
		}

		/// <summary>Saves <see cref="CurrentItem"/> with the values entered in the form.</summary>
		[Obsolete("Use CommandFactory")]
		public ContentItem Save(ContentItem item, ItemEditorVersioningMode mode)
		{
			EnsureChildControls();
			BinderContext = new CommandContext(GetDefinition(), item, "Unknown", Page.User, this, new N2.Edit.Web.PageValidator<CommandContext>(Page));
			item = EditAdapter.SaveItem(item, Editors, mode, Page.User);
			if (Saved != null)
				Saved.Invoke(this, new ItemEventArgs(item));
			return item;
		}

		/// <summary>Saves <see cref="CurrentItem"/> with the values entered in the form.</summary>
		/// <returns>The saved item.</returns>
		[Obsolete("Use CommandFactory")]
		public ContentItem Save()
		{
			CurrentItem = Save(CurrentItem, VersioningMode);
			return CurrentItem;
		}

		/// <summary>Updates the <see cref="CurrentItem"/> with the values entered in the form without saving it.</summary>
		public void Update()
		{
			UpdateObject(new CommandContext(GetDefinition(), CurrentItem, "Unknown", Page.User, this, new NullValidator<CommandContext>()));
		}

		public CommandContext CreateCommandContext()
		{
			return new CommandContext(Definition ?? GetDefinition(), CurrentItem, Interfaces.Editing, Page.User, this, new PageValidator<CommandContext>(Page));
		}

		#endregion

		#region IItemContainer Members

		ContentItem IItemContainer.CurrentItem
		{
			get { return CurrentItem; }
		}

		#endregion

		#region IItemEditor Members

		public event EventHandler<ItemEventArgs> Saved;

		#endregion

		#region IBinder<CommandContext> Members

		internal N2.Edit.Workflow.CommandContext BinderContext { get; set; }

		public bool UpdateObject(N2.Edit.Workflow.CommandContext value)
		{
			try
			{
				BinderContext = value;
				EnsureChildControls();
				if (ZoneName != null && ZoneName != value.Content.ZoneName)
					value.Content.ZoneName = ZoneName;
				foreach (string key in Editors.Select(e => e.PropertyName))
					BinderContext.GetDefinedDetails().Add(key);
				var modifiedDetails = EditAdapter.UpdateItem(value.Definition, value.Content, Editors, Page.User);
				if (modifiedDetails.Length == 0)
					return false;
				foreach (string detailName in modifiedDetails)
					BinderContext.GetUpdatedDetails().Add(detailName);
				BinderContext.RegisterItemToSave(value.Content);
				return true;
			}
			finally
			{
				BinderContext = null;
			}
		}

		public void UpdateInterface(N2.Edit.Workflow.CommandContext value)
		{
			try
			{
				BinderContext = value;
				EnsureChildControls();
				Engine.EditManager.UpdateEditors(GetDefinition(), value.Content, Editors, Page.User);
			}
			finally
			{
				BinderContext = null;
			}

		}

		#endregion

		#region IPlaceHolderAccessor Members

		public void AddPlaceHolder(string name, Control container)
		{
			placeholders[name] = container;
		}

		public Control GetPlaceHolder(string name)
		{
			Control container;
			placeholders.TryGetValue(name, out container);
			return container;
		}

		#endregion
	}
}